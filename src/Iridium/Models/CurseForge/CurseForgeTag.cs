using System.Text.Json.Serialization;

namespace Iridium.Models.CurseForge;


public sealed record CurseForgeCategory {
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("gameId")] public int? GameId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("iconUrl")] public string? IconUrl { get; init; }
    [JsonPropertyName("dateModified")] public DateTime? DateModified { get; init; }
    [JsonPropertyName("isClass")] public bool? IsClass { get; init; }
    [JsonPropertyName("classId")] public int? ClassId { get; init; }
    [JsonPropertyName("parentCategoryId")] public int? ParentCategoryId { get; init; }
    [JsonPropertyName("displayIndex")] public int? DisplayIndex { get; init; }
}


public sealed record CurseForgeGameVersion {
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("gameVersionId")] public int? GameVersionId { get; init; }
    [JsonPropertyName("versionString")] public string? VersionString { get; init; }
    [JsonPropertyName("jarDownloadUrl")] public string? JarDownloadUrl { get; init; }
    [JsonPropertyName("stable")] public bool? Stable { get; init; }
    [JsonPropertyName("approved")] public bool? Approved { get; init; }
    [JsonPropertyName("dateModified")] public DateTime? DateModified { get; init; }
    [JsonPropertyName("gameVersionTypeId")] public int? GameVersionTypeId { get; init; }
}
