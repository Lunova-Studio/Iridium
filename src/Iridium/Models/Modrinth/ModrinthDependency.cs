using System.Text.Json.Serialization;

namespace Iridium.Models.Modrinth;


public sealed record ModrinthDependency {
    [JsonPropertyName("version_id")] public string? VersionId { get; init; }
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }
    [JsonPropertyName("dependency_type")] public string? DependencyType { get; init; }
}
