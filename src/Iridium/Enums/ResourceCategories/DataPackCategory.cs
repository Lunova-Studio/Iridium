using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;

[ResourceCategoryType(ResourceType.DataPack)]
public enum DataPackCategory {
    [ModrinthCategory("worldgen")] WorldGen,
    [CurseForgeCategory(6951), ModrinthCategory("technology")] Technology,
    [ModrinthCategory("game-mechanics")] GameMechanics,
    [ModrinthCategory("transportation")] Transportation,
    [ModrinthCategory("storage")] Storage,
    [CurseForgeCategory(6952), ModrinthCategory("magic")] Magic,
    [CurseForgeCategory(6948), ModrinthCategory("adventure")] Adventure,
    [CurseForgeCategory(6949)] Fantasy,
    [ModrinthCategory("decoration")] Decoration,
    [ModrinthCategory("mobs")] Mobs,
    [CurseForgeCategory(6953), ModrinthCategory("utility")] Utility,
    [ModrinthCategory("equipment")] Equipment,
    [ModrinthCategory("optimization")] Optimization,
    [ModrinthCategory("social")] Social,
    [CurseForgeCategory(6950), ModrinthCategory("library")] Library,
    [CurseForgeCategory(6946)] ModRelated
}
