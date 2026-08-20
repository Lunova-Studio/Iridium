using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;

[ResourceCategoryType(ResourceType.World)]
public enum WorldCategory {
    [CurseForgeCategory(248)] Adventure,
    [CurseForgeCategory(249)] Creative,
    [CurseForgeCategory(250)] MiniGame,
    [CurseForgeCategory(251)] Parkour,
    [CurseForgeCategory(252)] Puzzle,
    [CurseForgeCategory(253)] Survival,
    [CurseForgeCategory(4464)] ModWorld
}
