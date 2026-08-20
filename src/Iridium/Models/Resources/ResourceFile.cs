using Iridium.Enums.Resources;
using Iridium.Interfaces.Resources;

namespace Iridium.Models.Resources;


public sealed record ResourceFile : IResourceFile {
    public required ResourceSource Source { get; init; }
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public string? Name { get; init; }
    public string? VersionNumber { get; init; }
    public string? Changelog { get; init; }
    public ReleaseType ReleaseType { get; init; }
    public DateTime? Published { get; init; }
    public long Downloads { get; init; }
    public IReadOnlyList<string> GameVersions { get; init; } = [];
    public IReadOnlyList<ResourceLoaderType> Loaders { get; init; } = [];
    public IResourceFileEntry? PrimaryFile { get; init; }
    public IReadOnlyList<IResourceFileEntry> Files { get; init; } = [];
    public IReadOnlyList<IResourceDependency> Dependencies { get; init; } = [];
}
