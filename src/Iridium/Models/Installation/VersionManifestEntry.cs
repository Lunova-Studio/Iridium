using System.Text.Json.Serialization;

namespace Iridium.Models.Installation;

public sealed record VersionManifestEntry {
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("time")] public DateTime Time { get; set; }
    [JsonPropertyName("releaseTime")] public DateTime ReleaseTime { get; set; }
}

[JsonSerializable(typeof(VersionManifestEntry))]
[JsonSerializable(typeof(IEnumerable<VersionManifestEntry>))]
public sealed partial class VersionManifestEntryContext : JsonSerializerContext;