using System.Text.Json.Serialization;

namespace Iridium.Models.Modrinth;


public sealed record ModrinthCategory {
    [JsonPropertyName("icon")] public string? Icon { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("project_type")] public string? ProjectType { get; init; }
    [JsonPropertyName("header")] public string? Header { get; init; }
}


public sealed record ModrinthLoader {
    [JsonPropertyName("icon")] public string? Icon { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("supported_project_types")] public List<string> SupportedProjectTypes { get; init; } = [];
}


public sealed record ModrinthGameVersion {
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("version_type")] public string? VersionType { get; init; }
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
    [JsonPropertyName("major")] public bool Major { get; init; }
}
