using System.Text.Json.Serialization;

namespace Iridium.Models.Authentication.Microsoft;

public record MinecraftPayload(string identityToken);

public record XblProperties {
    public required string SiteName { get; init; }
    public required string RpsTicket { get; init; }
    public required string AuthMethod { get; init; }
}

public record XstsProperties {
    public required string SandboxId { get; init; }
    public required string[] UserTokens { get; init; }
}

public record XblTokenPayload {
    public required string TokenType { get; init; }
    public required string RelyingParty { get; init; }
    public required XblProperties Properties { get; init; }
}

public record XstsTokenPayload {
    public required string TokenType { get; init; }
    public required string RelyingParty { get; init; }
    public required XstsProperties Properties { get; init; }
}

public record RefreshTokenPayload {
    [JsonPropertyName("client_id")] public required string ClientId { get; init; }
    [JsonPropertyName("grant_type")] public required string GrantType { get; init; }
    [JsonPropertyName("refresh_token")] public required string RefreshToken { get; init; }
}

[JsonSerializable(typeof(XblTokenPayload))]
[JsonSerializable(typeof(XstsTokenPayload))]
[JsonSerializable(typeof(MinecraftPayload))]
public sealed partial class MicrosoftRequestPayloadContext : JsonSerializerContext;