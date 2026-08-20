using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;

[ResourceCategoryType(ResourceType.Mod)]
public enum ModCategory {
    [CurseForgeCategory(406), ModrinthCategory("worldgen")] WorldGen,
    [CurseForgeCategory(407)] Biomes,
    [CurseForgeCategory(410)] Dimensions,
    [CurseForgeCategory(408)] OresAndResources,
    [CurseForgeCategory(409)] Structures,
    [CurseForgeCategory(412), ModrinthCategory("technology")] Technology,
    [CurseForgeCategory(415)] PipesAndLogistics,
    [CurseForgeCategory(4843)] Automation,
    [CurseForgeCategory(417)] Energy,
    [CurseForgeCategory(4558)] Redstone,
    [ModrinthCategory("game-mechanics")] GameMechanics,
    [CurseForgeCategory(436), ModrinthCategory("food")] Food,
    [CurseForgeCategory(416)] Farming,
    [CurseForgeCategory(414), ModrinthCategory("transportation")] Transportation,
    [CurseForgeCategory(420), ModrinthCategory("storage")] Storage,
    [CurseForgeCategory(419), ModrinthCategory("magic")] Magic,
    [CurseForgeCategory(422), ModrinthCategory("adventure")] Adventure,
    [CurseForgeCategory(424), ModrinthCategory("decoration")] Decoration,
    [CurseForgeCategory(411), ModrinthCategory("mobs")] Mobs,
    [CurseForgeCategory(434), ModrinthCategory("equipment")] Equipment,
    [CurseForgeCategory(6814), ModrinthCategory("optimization")] Optimization,
    [CurseForgeCategory(9026)] Creative,
    [CurseForgeCategory(423)] Display,
    [CurseForgeCategory(435), ModrinthCategory("social")] Social,
    [CurseForgeCategory(5191)] Tweaks,
    [ModrinthCategory("utility")] Utility,
    [CurseForgeCategory(421), ModrinthCategory("library")] Library
}
