using Iridium.Enums.Resources;

namespace Iridium.Models.Resources;


public sealed record ResourceHit {
    public required ResourceSource Source { get; init; }
    public required string Id { get; init; }
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? IconUrl { get; init; }
    public string? Author { get; init; }
    public ResourceType Type { get; init; }
    public long Downloads { get; init; }
    public long Follows { get; init; }
    public DateTime? DateCreated { get; init; }
    public DateTime? DateModified { get; init; }
    public IReadOnlyList<ResourceCategory> Categories { get; init; } = [];
    public IReadOnlyList<string> GameVersions { get; init; } = [];
    public IReadOnlyList<ResourceLoaderType> Loaders { get; init; } = [];
    public IReadOnlyList<string> Screenshots { get; init; } = [];
    public string? Translation { get; init; }
    public string? WebsiteUrl { get; init; }
}
