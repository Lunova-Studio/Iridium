using System.Text.Json.Serialization;

namespace Iridium.Models.Modrinth;


public sealed record ModrinthVersion {
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }
    [JsonPropertyName("author_id")] public string? AuthorId { get; init; }
    [JsonPropertyName("featured")] public bool Featured { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version_number")] public string? VersionNumber { get; init; }
    [JsonPropertyName("changelog")] public string? Changelog { get; init; }
    [JsonPropertyName("changelog_url")] public string? ChangelogUrl { get; init; }
    [JsonPropertyName("date_published")] public DateTime? DatePublished { get; init; }
    [JsonPropertyName("downloads")] public long Downloads { get; init; }
    [JsonPropertyName("version_type")] public string? VersionType { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("game_versions")] public List<string> GameVersions { get; init; } = [];
    [JsonPropertyName("loaders")] public List<string> Loaders { get; init; } = [];
    [JsonPropertyName("files")] public List<ModrinthFile> Files { get; init; } = [];
    [JsonPropertyName("dependencies")] public List<ModrinthDependency> Dependencies { get; init; } = [];
}
