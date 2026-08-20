using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;

[ResourceCategoryType(ResourceType.ResourcePack)]
public enum ResourcePackCategory {
    [CurseForgeCategory(403), ModrinthCategory("vanilla-like")] VanillaLike,
    [CurseForgeCategory(400), ModrinthCategory("realistic")] Realistic,
    [CurseForgeCategory(401)] Modern,
    [CurseForgeCategory(402)] Medieval,
    [CurseForgeCategory(399)] Steampunk,
    [CurseForgeCategory(5244), ModrinthCategory("fonts")] Fonts,
    [CurseForgeCategory(404)] Animated,
    [CurseForgeCategory(4465), ModrinthCategory("modded")] ModSupport,
    [CurseForgeCategory(393), ModrinthCategory("16x")] Resolution16x,
    [CurseForgeCategory(394), ModrinthCategory("32x")] Resolution32x,
    [ModrinthCategory("48x")] Resolution48x,
    [CurseForgeCategory(395), ModrinthCategory("64x")] Resolution64x,
    [CurseForgeCategory(396), ModrinthCategory("128x")] Resolution128x,
    [CurseForgeCategory(397), ModrinthCategory("256x")] Resolution256x,
    [CurseForgeCategory(398), ModrinthCategory("512x+")] Resolution512x,
    [ModrinthCategory("audio")] Audio,
    [ModrinthCategory("models")] Models,
    [ModrinthCategory("gui")] Gui,
    [ModrinthCategory("locale")] Locale,
    [ModrinthCategory("core-shaders")] CoreShaders,
    [ModrinthCategory("themed")] Themed,
    [ModrinthCategory("simplistic")] Simplistic,
    [ModrinthCategory("tweaks")] Tweaks,
    [ModrinthCategory("cursed")] Cursed,
    [ModrinthCategory("entities")] Entities,
    [ModrinthCategory("decoration")] Decoration,
    [ModrinthCategory("combat")] Combat,
    [ModrinthCategory("utility")] Utility
}
