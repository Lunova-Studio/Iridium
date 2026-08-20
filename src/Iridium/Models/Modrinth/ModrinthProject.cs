using System.Text.Json.Serialization;

namespace Iridium.Models.Modrinth;


public sealed record ModrinthProject {
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("body")] public string? Body { get; init; }
    [JsonPropertyName("project_type")] public string? ProjectType { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("downloads")] public long Downloads { get; init; }
    [JsonPropertyName("followers")] public long Followers { get; init; }
    [JsonPropertyName("categories")] public List<string> Categories { get; init; } = [];
    [JsonPropertyName("additional_categories")] public List<string> AdditionalCategories { get; init; } = [];
    [JsonPropertyName("game_versions")] public List<string> GameVersions { get; init; } = [];
    [JsonPropertyName("loaders")] public List<string> Loaders { get; init; } = [];
    [JsonPropertyName("versions")] public List<string> Versions { get; init; } = [];
    [JsonPropertyName("icon_url")] public string? IconUrl { get; init; }
    [JsonPropertyName("issues_url")] public string? IssuesUrl { get; init; }
    [JsonPropertyName("source_url")] public string? SourceUrl { get; init; }
    [JsonPropertyName("wiki_url")] public string? WikiUrl { get; init; }
    [JsonPropertyName("discord_url")] public string? DiscordUrl { get; init; }
    [JsonPropertyName("published")] public DateTime? Published { get; init; }
    [JsonPropertyName("updated")] public DateTime? Updated { get; init; }
    [JsonPropertyName("approved")] public DateTime? Approved { get; init; }
    [JsonPropertyName("license")] public ModrinthLicense? License { get; init; }
    [JsonPropertyName("gallery")] public List<ModrinthGalleryItem> Gallery { get; init; } = [];
    [JsonPropertyName("team")] public string? TeamId { get; init; }
    [JsonPropertyName("client_side")] public string? ClientSide { get; init; }
    [JsonPropertyName("server_side")] public string? ServerSide { get; init; }
    [JsonPropertyName("donation_urls")] public List<ModrinthDonationUrl> DonationUrls { get; init; } = [];
}
