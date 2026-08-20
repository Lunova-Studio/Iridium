using Iridium.Enums.Resources;

namespace Iridium.Models.Resources;


public readonly record struct ResourceCategory {

    public ResourceType Type { get; init; }


    public string Name { get; init; }


    public string? DisplayName { get; init; }


    public int? CurseForgeId { get; init; }


    public string? ModrinthSlug { get; init; }
}
