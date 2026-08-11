using System.Net.Http.Json;
using System.Text.Json;
using Flurl.Http;
using Iridium.Models.Authentication;
using Iridium.Models.Authentication.Microsoft;

namespace Iridium.Services.Authentication;

public sealed class MicrosoftAuthenticator(string clientId) {
    private readonly IEnumerable<string> _scopes = ["XboxLive.signin", "offline_access", "openid", "profile", "email"];

    /// <summary>
    /// Asynchronously authenticates the Microsoft account using device flow authentication.
    /// </summary>
    /// <param name="deviceCode">The action to be performed with the device code response.</param>
    /// <param name="cancellationToken">The cancellation token source to be used to cancel the operation.</param>
    /// <returns>A Task that represents the asynchronous operation. The task result contains the OAuth2 token response.</returns>
    public async Task<OAuth2TokenResponse> DeviceFlowAuthAsync(Action<DeviceCodeResponse> deviceCode, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        var codeResponse = await GetDeviceCodeAsync(cancellationToken);
        deviceCode.Invoke(codeResponse);
        
        var interval = TimeSpan.FromSeconds(codeResponse.Interval);
        var requestParams = new Dictionary<string, string> {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = clientId,
            ["device_code"] = codeResponse.DeviceCode
        };
        
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(codeResponse.ExpiresIn));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var combinedToken = linkedCts.Token;
        
        while (!combinedToken.IsCancellationRequested) {
            using var response = await "https://login.microsoftonline.com/consumers/oauth2/v2.0/token"
                .OnError(x => x.ExceptionHandled = true)
                .PostUrlEncodedAsync(requestParams, cancellationToken: combinedToken);

            using var doc = await JsonDocument.ParseAsync(
                await response.GetStreamAsync(),
                cancellationToken: combinedToken);
            
            var root = doc.RootElement;
            if (root.TryGetProperty("access_token"u8, out var accessToken)) {
                return new OAuth2TokenResponse {
                    AccessToken = accessToken.GetString(),
                    RefreshToken = root.GetProperty("refresh_token"u8).GetString(),
                    ExpiresIn = root.GetProperty("expires_in"u8).GetInt32()
                };
            }
    
            if (root.TryGetProperty("error"u8, out var error)) {
                switch (error.GetString()) {
                    case "slow_down":
                        interval += TimeSpan.FromSeconds(5);
                        break;
                    case "authorization_pending":
                        break;
                    case "expired_token":
                        throw new TimeoutException("The device code has expired. Please re-initiate authorization.");
                    case "access_denied":
                        throw new UnauthorizedAccessException("The user denied authorization.");
                    default:
                        throw new InvalidOperationException($"Authorization failed: {error.GetString()}");
                }
            }
            
            await Task.Delay(interval, combinedToken);
        }
        
        throw new TimeoutException("Device flow authentication timed out."); 
    }

    public async Task<MicrosoftAccount> AuthenticateAsync(OAuth2TokenResponse oAuth2Token, CancellationToken cancellationToken = default) {
        if (oAuth2Token is null)
            ArgumentException.ThrowIfNullOrEmpty(nameof(oAuth2Token));

        using var xblToken = await GetXblTokenAsync(oAuth2Token!.AccessToken, cancellationToken);
        using var xsts = await GetXstsTokenAsync(xblToken.RootElement, cancellationToken);
        using var minecraftAccessToken = await GetMinecraftAccessTokenAsync((xblToken.RootElement, xsts.RootElement), cancellationToken);
        var profile = await GetMinecraftProfileAsync(minecraftAccessToken.RootElement.GetProperty("access_token"u8).GetString(),
            oAuth2Token.RefreshToken, cancellationToken);

        return profile;
    }
    
    public async Task<MicrosoftAccount> RefreshAsync(MicrosoftAccount account, CancellationToken cancellationToken = default) {
        Dictionary<string, string> payload = new() {
            ["client_id"] = clientId,
            ["refresh_token"] = account.RefreshToken,
            ["grant_type"] = "refresh_token"
        };

        var result = await "https://login.live.com/oauth20_token.srf"
            .PostAsync(new FormUrlEncodedContent(payload), 
                cancellationToken: cancellationToken);

        await using var jsonStream = await result.GetStreamAsync();
        var response = await JsonSerializer.DeserializeAsync(jsonStream, 
            OAuth2TokenResponseContext.Default.OAuth2TokenResponse, cancellationToken);
        
        return await AuthenticateAsync(response, cancellationToken);
    }
    
    private async Task<DeviceCodeResponse> GetDeviceCodeAsync(CancellationToken  cancellationToken) {
        var parameters = new Dictionary<string, string> {
            ["client_id"] = clientId,
            ["tenant"] = "/consumers",
            ["scope"] = string.Join(" ", _scopes)
        };
        
        var codeResponse = await "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode"
            .PostUrlEncodedAsync(parameters, 
                cancellationToken: cancellationToken);
        
        await using var codeStream = await codeResponse
            .ResponseMessage.Content
            .ReadAsStreamAsync(cancellationToken);

        var codeContext = await JsonSerializer.DeserializeAsync(codeStream,
            DeviceCodeResponseContext.Default.DeviceCodeResponse,
                cancellationToken); 
        
        return codeContext;
    }
    
    private static async Task<JsonDocument> GetXblTokenAsync(string token, CancellationToken cancellationToken = default) {
        var xblContent = new XblTokenPayload {
            Properties = new XblProperties {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={token}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };
        
        using var xblJsonReq = await "https://user.auth.xboxlive.com/user/authenticate"
            .PostAsync(JsonContent.Create(xblContent,
                MicrosoftRequestPayloadContext.Default.XblTokenPayload),
                cancellationToken: cancellationToken);

        return await JsonDocument.ParseAsync(await xblJsonReq.GetStreamAsync(),
            cancellationToken: cancellationToken);
    }
    
    private static async Task<JsonDocument> GetXstsTokenAsync(JsonElement xblTokenNode, CancellationToken cancellationToken = default) {
        var xstsContent = new XstsTokenPayload {
            Properties = new XstsProperties {
                SandboxId = "RETAIL",
                UserTokens = [xblTokenNode.GetProperty("Token"u8).GetString()]
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        };
        
        using var xstsJsonReq = await "https://xsts.auth.xboxlive.com/xsts/authorize"
            .PostAsync(JsonContent.Create(xstsContent, 
                MicrosoftRequestPayloadContext.Default.XstsTokenPayload),
                cancellationToken: cancellationToken);

        return await JsonDocument.ParseAsync(await xstsJsonReq.GetStreamAsync(),
            cancellationToken: cancellationToken);
    }

    private static async Task<JsonDocument> GetMinecraftAccessTokenAsync((JsonElement xblTokenNode, JsonElement xstsTokenNode) nodes, CancellationToken cancellationToken = default) {
        var xstsToken = nodes.xstsTokenNode
            .GetProperty("Token"u8)
            .GetString();
        
        var uhsToken = nodes.xblTokenNode
            .GetProperty("DisplayClaims"u8)
            .GetProperty("xui"u8)[0] // get first
            .GetProperty("uhs"u8)
            .GetString();

        var payload = new MinecraftPayload($"XBL3.0 x={uhsToken};{xstsToken}");
        using var mcTokenReq = await "https://api.minecraftservices.com/authentication/login_with_xbox"
            .PostAsync(JsonContent.Create(payload,
                MicrosoftRequestPayloadContext.Default.MinecraftPayload),
                cancellationToken: cancellationToken);
        
        return await JsonDocument.ParseAsync(await mcTokenReq.GetStreamAsync(), 
            cancellationToken: cancellationToken);
    }

    private static async Task<MicrosoftAccount> GetMinecraftProfileAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default) {
        using var profileRes = await "https://api.minecraftservices.com/minecraft/profile"
            .WithHeader("Authorization", $"Bearer {accessToken}")
            .GetAsync(cancellationToken: cancellationToken);
        
        try {
            using var profileNode = await JsonDocument.ParseAsync(await profileRes.GetStreamAsync(),
                cancellationToken: cancellationToken);
            
            var name = profileNode.RootElement.GetProperty("name"u8).GetString();
            var uuid = profileNode.RootElement.GetProperty("id"u8).GetString(); // fix guid parse error
            
            return new MicrosoftAccount(name, Guid.Parse(uuid!), accessToken, refreshToken, DateTime.Now);
        }
        catch (Exception e) {
            throw new InvalidOperationException("Failed to retrieve Minecraft profile", e);
        }
    }
}