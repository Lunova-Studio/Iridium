using Iridium.Enums.Resources;

namespace Iridium.Models.Resources;


public sealed class ResourceSearchOptions {

    public ResourceSource Source { get; init; } = ResourceSource.All;


    public ResourceType Type { get; init; } = ResourceType.Mod;


    public string? Query { get; init; }


    public IReadOnlyList<ResourceCategory> Tags { get; init; } = [];


    public string? GameVersion { get; init; }


    public ResourceLoaderType Loader { get; init; } = ResourceLoaderType.Any;


    public ResourceSort Sort { get; init; } = ResourceSort.Relevance;


    public SortOrder SortOrder { get; init; } = SortOrder.Desc;


    public int Page { get; init; } = 1;


    public int PageSize { get; init; } = 40;
}
