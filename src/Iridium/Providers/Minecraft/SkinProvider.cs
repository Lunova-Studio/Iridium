using System.Text.Json;
using Flurl.Http;
using Iridium.Models.Authentication;

namespace Iridium.Providers.Minecraft;

public static class SkinProvider {
    private const string MicrosoftProfileUrlTemplate = "https://sessionserver.mojang.com/session/minecraft/profile/{0}";
    private const string YggdrasilProfileUrlTemplate = "{0}/sessionserver/session/minecraft/profile/{1}";

    public static Task<Stream> GetMicrosoftSkinDataAsync(MicrosoftAccount account,
        CancellationToken cancellationToken = default)
        => GetSkinDataAsync(string.Format(MicrosoftProfileUrlTemplate, account.Uuid.ToString("N")), cancellationToken);

    public static Task<Stream> GetYggdrasilSkinDataAsync(YggdrasilAccount account,
        CancellationToken cancellationToken = default)
        => GetSkinDataAsync(string.Format(YggdrasilProfileUrlTemplate, account.YggdrasilServerUrl,
            account.Uuid.ToString("N")), cancellationToken);

    private static async Task<Stream> GetSkinDataAsync(string profileUrl, CancellationToken cancellationToken) {
        await using var stream = await profileUrl.GetStreamAsync(cancellationToken: cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = doc.RootElement;
        if (!root.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Array ||
            properties.GetArrayLength() == 0)
            throw new InvalidOperationException("The profile does not contain any skin properties.");

        var base64 = properties[0].GetProperty("value").GetString()
                     ?? throw new InvalidOperationException("The profile does not contain a valid skin property value.");
        using var skinDoc = JsonDocument.Parse(Convert.FromBase64String(base64));

        if (!skinDoc.RootElement.TryGetProperty("textures", out var textures) ||
            !textures.TryGetProperty("SKIN", out var skin) ||
            !skin.TryGetProperty("url", out var url))
            throw new InvalidOperationException("The profile does not contain a skin texture.");

        return await url.GetString().GetStreamAsync(cancellationToken: cancellationToken);
    }
}
