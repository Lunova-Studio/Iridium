using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;

[ResourceCategoryType(ResourceType.Modpack)]
public enum ModpackCategory {
    [CurseForgeCategory(4484)] Multiplayer,
    [CurseForgeCategory(4479), ModrinthCategory("challenging")] Hardcore,
    [CurseForgeCategory(4483), ModrinthCategory("combat")] Combat,
    [CurseForgeCategory(4478), ModrinthCategory("quests")] Quests,
    [CurseForgeCategory(4472), ModrinthCategory("technology")] Technology,
    [CurseForgeCategory(4473), ModrinthCategory("magic")] Magic,
    [CurseForgeCategory(4475), ModrinthCategory("adventure")] Adventure,
    [CurseForgeCategory(4476)] Exploration,
    [CurseForgeCategory(4477)] MiniGame,
    [CurseForgeCategory(4471)] SciFi,
    [CurseForgeCategory(4736)] Skyblock,
    [CurseForgeCategory(5128)] VanillaPlus,
    [CurseForgeCategory(4487)] Ftb,
    [CurseForgeCategory(4480)] MapBased,
    [CurseForgeCategory(4481), ModrinthCategory("lightweight")] SmallLight,
    [CurseForgeCategory(4482)] ExtraLarge,
    [ModrinthCategory("kitchen-sink")] KitchenSink,
    [ModrinthCategory("optimization")] Optimization
}
