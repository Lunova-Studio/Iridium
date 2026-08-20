using Iridium.Enums.Resources;
using Iridium.Extensions.Resources;
using Iridium.Models.Resources;
using Iridium.Providers.CurseForge;
using Iridium.Providers.Modrinth;

namespace Iridium.Services.Resources;


public sealed class ResourceCategoryService {
    private readonly ModrinthClient _modrinth;
    private readonly CurseForgeClient _curseForge;

    public ResourceCategoryService(ModrinthClient? modrinth = null, CurseForgeClient? curseForge = null) {
        _modrinth = modrinth ?? new ModrinthClient();
        _curseForge = curseForge ?? new CurseForgeClient();
    }


    public async Task<IReadOnlyList<ResourceCategory>> GetCategoriesAsync(ResourceType type,
        ResourceSource source = ResourceSource.All, bool includeStatic = true,
        CancellationToken cancellationToken = default) {
        var results = new List<ResourceCategory>();

        if (source.HasFlag(ResourceSource.Modrinth) && type != ResourceType.World) {
            var projectType = type.ToModrinthProjectType();
            var categories = await _modrinth.GetCategoriesAsync(cancellationToken);
            results.AddRange(categories
                .Where(category => string.Equals(category.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
                .Select(category => new ResourceCategory {
                    Type = type,
                    Name = category.Name ?? string.Empty,
                    DisplayName = category.Name,
                    ModrinthSlug = category.Name
                }));
        }

        if (source.HasFlag(ResourceSource.CurseForge)) {
            var classId = type.ToCurseForgeClassId();
            var categories = await _curseForge.GetCategoriesAsync(cancellationToken);
            results.AddRange(categories
                .Where(category => category.ClassId == classId)
                .Select(category => new ResourceCategory {
                    Type = type,
                    Name = category.Slug ?? string.Empty,
                    DisplayName = category.Name,
                    CurseForgeId = category.Id
                }));
        }

        if (includeStatic)
            results.AddRange(type.GetStaticCategories());

        return results.Distinct().ToArray();
    }
}
