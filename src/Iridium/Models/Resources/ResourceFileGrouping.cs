using Iridium.Enums.Resources;
using Iridium.Interfaces.Resources;

namespace Iridium.Models.Resources;


public sealed record ResourceFileGrouping {

    public IReadOnlyList<ResourceVersionGroup> Versions { get; init; } = [];


    public IReadOnlyList<IResourceFile> Unversioned { get; init; } = [];


    public ResourceVersionGroup? Recommended { get; init; }
}


public sealed record ResourceVersionGroup {
    public string GameVersion { get; init; }
    public IReadOnlyList<ResourceLoaderGroup> Loaders { get; init; } = [];
    public ResourceLoaderGroup? Recommended { get; init; }
}


public sealed record ResourceLoaderGroup {
    public ResourceLoaderType Loader { get; init; }
    public IReadOnlyList<IResourceFile> Files { get; init; } = [];
    public IResourceFile? Recommended { get; init; }
}
