using System.Net.Http.Json;
using System.Text.Json;
using Flurl;
using Flurl.Http;
using Iridium.Models.Authentication;
using Iridium.Models.Authentication.Yggdrasil;

namespace Iridium.Services.Authentication;

public sealed class YggdrasilAuthenticator(string url) {
    public async Task<YggdrasilAccount> RefreshAsync(YggdrasilAccount account, CancellationToken cancellationToken = default) {
        var payload = new YggdrasilRefreshPayload {
            RequestUser = true,
            AccessToken = account.AccessToken,
            ClientToken = account.ClientToken,
            SelectedProfile = new SelectedProfile {
                Name = account.Name,
                Id = account.Uuid.ToString("N")
            }
        };

        using var responseMessage = await url.AppendPathSegments("authserver", "refresh")
            .PostAsync(JsonContent
                .Create(payload,
                    YggdrasilRequestPayloadContext.Default.YggdrasilRefreshPayload),
                cancellationToken: cancellationToken);

        await using var json = await responseMessage.GetStreamAsync();
        var entry = await JsonSerializer.DeserializeAsync(json,
            YggdrasilResponseContext.Default.YggdrasilResponse,
            cancellationToken);
        
        var profile = entry!.SelectedProfile;
        return new YggdrasilAccount(profile.Name, Guid.Parse(profile.Id), entry.AccessToken, url, entry.ClientToken);
    }
    
    public async Task<IReadOnlyList<YggdrasilAccount>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) {
        var payload = new YggdrasilAuthenticatePayload {
            ClientToken = Guid.NewGuid().ToString("N"),
            Username = email,
            Password = password,
            RequestUser = false
        };

        using var responseMessage = await url.AppendPathSegments("authserver", "authenticate")
            .PostAsync(JsonContent.Create(payload, 
                    YggdrasilRequestPayloadContext.Default.YggdrasilAuthenticatePayload), 
                cancellationToken: cancellationToken);

        await using var json = await responseMessage.GetStreamAsync();
        var entry = await JsonSerializer.DeserializeAsync(json,
            YggdrasilResponseContext.Default.YggdrasilResponse,
            cancellationToken);

        return entry!.AvailableProfiles.Select(profile =>
            new YggdrasilAccount(profile.Name, Guid.Parse(profile.Id), entry.AccessToken, url, entry.ClientToken)) 
                .ToList()
                .AsReadOnly();
    }
}