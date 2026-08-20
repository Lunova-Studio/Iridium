using System.Text.Json.Serialization;

namespace Iridium.Models.Modrinth;


public sealed record ModrinthFile {
    [JsonPropertyName("hashes")] public ModrinthFileHashes? Hashes { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("filename")] public string? FileName { get; init; }
    [JsonPropertyName("primary")] public bool Primary { get; init; }
    [JsonPropertyName("size")] public long Size { get; init; }
    [JsonPropertyName("file_type")] public string? FileType { get; init; }
}

public sealed record ModrinthFileHashes {
    [JsonPropertyName("sha1")] public string? Sha1 { get; init; }
    [JsonPropertyName("sha512")] public string? Sha512 { get; init; }
}
