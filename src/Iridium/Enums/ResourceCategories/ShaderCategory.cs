using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;

[ResourceCategoryType(ResourceType.Shader)]
public enum ShaderCategory {
    [CurseForgeCategory(6553), ModrinthCategory("realistic")] Realistic,
    [CurseForgeCategory(6554), ModrinthCategory("fantasy")] Fantasy,
    [CurseForgeCategory(6555), ModrinthCategory("vanilla-like")] VanillaLike,
    [ModrinthCategory("semi-realistic")] SemiRealistic,
    [ModrinthCategory("cartoon")] Cartoon,
    [ModrinthCategory("colored-lighting")] ColoredLighting,
    [ModrinthCategory("path-tracing")] PathTracing,
    [ModrinthCategory("pbr")] Pbr,
    [ModrinthCategory("reflections")] Reflections,
    [ModrinthCategory("potato")] Potato,
    [ModrinthCategory("low")] Low,
    [ModrinthCategory("medium")] Medium,
    [ModrinthCategory("high")] High
}
