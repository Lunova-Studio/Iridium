using System.Text.Json.Serialization;

namespace Iridium.Models.CurseForge;


public sealed record CurseForgeProject {
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("gameId")] public int? GameId { get; init; }
    [JsonPropertyName("classId")] public int? ClassId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("links")] public CurseForgeLinks? Links { get; init; }
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("status")] public int? Status { get; init; }
    [JsonPropertyName("downloadCount")] public long? DownloadCount { get; init; }
    [JsonPropertyName("isFeatured")] public bool? IsFeatured { get; init; }
    [JsonPropertyName("primaryLanguage")] public string? PrimaryLanguage { get; init; }
    [JsonPropertyName("authors")] public List<CurseForgeAuthor> Authors { get; init; } = [];
    [JsonPropertyName("logo")] public CurseForgeAsset? Logo { get; init; }
    [JsonPropertyName("screenshots")] public List<CurseForgeAsset> Screenshots { get; init; } = [];
    [JsonPropertyName("mainFileId")] public long? MainFileId { get; init; }
    [JsonPropertyName("dateCreated")] public DateTime? DateCreated { get; init; }
    [JsonPropertyName("dateModified")] public DateTime? DateModified { get; init; }
    [JsonPropertyName("dateReleased")] public DateTime? DateReleased { get; init; }
    [JsonPropertyName("latestFiles")] public List<CurseForgeFile> LatestFiles { get; init; } = [];
    [JsonPropertyName("categories")] public List<CurseForgeCategory> Categories { get; init; } = [];
    [JsonPropertyName("gameVersionLatestFiles")] public List<CurseForgeFileIndex> GameVersionLatestFiles { get; init; } = [];
    [JsonPropertyName("latestFilesIndexes")] public List<CurseForgeFileIndex> LatestFilesIndexes { get; init; } = [];
    [JsonPropertyName("allowModDistribution")] public bool? AllowModDistribution { get; init; }
}

public sealed record CurseForgeLinks {
    [JsonPropertyName("websiteUrl")] public string? WebsiteUrl { get; init; }
    [JsonPropertyName("wikiUrl")] public string? WikiUrl { get; init; }
    [JsonPropertyName("issuesUrl")] public string? IssuesUrl { get; init; }
    [JsonPropertyName("sourceUrl")] public string? SourceUrl { get; init; }
}

public sealed record CurseForgeAuthor {
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
}

public sealed record CurseForgeAsset {
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
}

public sealed record CurseForgeFileIndex {
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; init; }
    [JsonPropertyName("fileId")] public long? FileId { get; init; }
    [JsonPropertyName("filename")] public string? FileName { get; init; }
    [JsonPropertyName("releaseType")] public int? ReleaseType { get; init; }
    [JsonPropertyName("gameVersionTypeId")] public int? GameVersionTypeId { get; init; }
    [JsonPropertyName("modLoader")] public int? ModLoader { get; init; }
}
