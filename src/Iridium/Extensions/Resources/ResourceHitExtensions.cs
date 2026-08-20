using Iridium.Enums.Resources;
using Iridium.Models.Resources;
using Iridium.Services.Resources;

namespace Iridium.Extensions.Resources;


public static class ResourceHitExtensions {

    public static async Task<IReadOnlyList<ResourceHit>> EnrichWithTranslationsAsync(
        this IReadOnlyList<ResourceHit> hits, ResourceTranslationService translationService,
        CancellationToken cancellationToken = default) {
        var modrinthIds = hits.Where(hit => hit.Source == ResourceSource.Modrinth)
            .Select(hit => hit.Id).ToArray();
        var curseForgeIds = hits.Where(hit => hit.Source == ResourceSource.CurseForge)
            .Select(hit => hit.Id).ToArray();

        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        if (modrinthIds.Length > 0) {
            var result = await translationService.GetTranslationsAsync(ResourceSource.Modrinth,
                modrinthIds, cancellationToken: cancellationToken);
            foreach (var pair in result)
                translations[$"{ResourceSource.Modrinth}:{pair.Key}"] = pair.Value;
        }

        if (curseForgeIds.Length > 0) {
            var result = await translationService.GetTranslationsAsync(ResourceSource.CurseForge,
                curseForgeIds, cancellationToken: cancellationToken);
            foreach (var pair in result)
                translations[$"{ResourceSource.CurseForge}:{pair.Key}"] = pair.Value;
        }

        if (translations.Count == 0)
            return hits;

        return hits.Select(hit => translations.TryGetValue($"{hit.Source}:{hit.Id}", out var translated)
                ? hit with { Translation = translated }
                : hit)
            .ToArray();
    }
}
