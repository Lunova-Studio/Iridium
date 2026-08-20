using System.Text.Json.Serialization;

namespace Iridium.Models.Modrinth;


public sealed record ModrinthSearchHit {
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }
    [JsonPropertyName("project_type")] public string? ProjectType { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("author")] public string? Author { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("categories")] public List<string> Categories { get; init; } = [];
    [JsonPropertyName("display_categories")] public List<string> DisplayCategories { get; init; } = [];
    [JsonPropertyName("versions")] public List<string> Versions { get; init; } = [];
    [JsonPropertyName("downloads")] public long Downloads { get; init; }
    [JsonPropertyName("follows")] public long Follows { get; init; }
    [JsonPropertyName("icon_url")] public string? IconUrl { get; init; }
    [JsonPropertyName("date_created")] public DateTime? DateCreated { get; init; }
    [JsonPropertyName("date_modified")] public DateTime? DateModified { get; init; }
    [JsonPropertyName("latest_version")] public string? LatestVersion { get; init; }
    [JsonPropertyName("license")] public string? License { get; init; }
    [JsonPropertyName("client_side")] public string? ClientSide { get; init; }
    [JsonPropertyName("server_side")] public string? ServerSide { get; init; }
    [JsonPropertyName("gallery")] public List<string> Gallery { get; init; } = [];
    [JsonPropertyName("featured_gallery")] public string? FeaturedGallery { get; init; }
}


public sealed record ModrinthSearchResult {
    [JsonPropertyName("hits")] public List<ModrinthSearchHit> Hits { get; init; } = [];
    [JsonPropertyName("offset")] public int Offset { get; init; }
    [JsonPropertyName("limit")] public int Limit { get; init; }
    [JsonPropertyName("total_hits")] public long TotalHits { get; init; }
}
