using Iridium.Enums.Resources;
using Iridium.Models.Resources;

namespace Iridium.Services.Resources;


public static class ResourceMerger {
    private const double ModrinthPreferredWeight = 7;
    private const double CurseForgePreferredWeight = 10;
    private const double DefaultPreferredWeight = 5;


    public static bool IsLike(ResourceHit left, ResourceHit right) {
        if (left.Source == right.Source)
            return false;
        if (string.IsNullOrWhiteSpace(left.Title) || string.IsNullOrWhiteSpace(right.Title))
            return false;

        if (!string.IsNullOrWhiteSpace(left.Slug) && !string.IsNullOrWhiteSpace(right.Slug) &&
            string.Equals(left.Slug, right.Slug, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(NormalizeName(left.Title), NormalizeName(right.Title),
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (left.Loaders.Count > 0 && right.Loaders.Count > 0 &&
            !left.Loaders.Intersect(right.Loaders).Any())
            return false;

        if (left.GameVersions.Count > 0 && right.GameVersions.Count > 0 &&
            !left.GameVersions.Intersect(right.GameVersions, StringComparer.OrdinalIgnoreCase).Any())
            return false;

        if (left.DateModified is { } leftModified && right.DateModified is { } rightModified &&
            Math.Abs((leftModified - rightModified).TotalDays) > 7)
            return false;

        return true;
    }


    public static IReadOnlyList<ResourceHit> MergeAndSort(IEnumerable<ResourceHit> hits,
        ResourceType type, ResourceSort sort, string? query) {
        var merged = new List<ResourceHit>();
        foreach (var hit in hits) {
            var duplicateIndex = merged.FindIndex(existing => IsLike(existing, hit));
            if (duplicateIndex < 0) {
                merged.Add(hit);
                continue;
            }

            if (GetSourcePreference(hit.Type) > GetSourcePreference(merged[duplicateIndex].Type))
                merged[duplicateIndex] = hit;
        }

        switch (sort) {
            case ResourceSort.Downloads or ResourceSort.TotalDownloads:
                merged.Sort((left, right) => right.Downloads.CompareTo(left.Downloads));
                break;
            case ResourceSort.Follows:
                merged.Sort((left, right) => right.Follows.CompareTo(left.Follows));
                break;
            case ResourceSort.Newest or ResourceSort.ReleasedDate:
                merged.Sort((left, right) =>
                    (right.DateCreated ?? DateTime.MinValue).CompareTo(left.DateCreated ?? DateTime.MinValue));
                break;
            case ResourceSort.Updated or ResourceSort.LastUpdated:
                merged.Sort((left, right) =>
                    (right.DateModified ?? DateTime.MinValue).CompareTo(left.DateModified ?? DateTime.MinValue));
                break;
            default:
                var ranked = merged.Select((hit, index) => (Hit: hit, Index: index, Score: ComputeRelevanceScore(hit, query)))
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Index)
                    .Select(item => item.Hit)
                    .ToArray();
                merged = new List<ResourceHit>(ranked);
                break;
        }

        return merged;
    }

    private static double GetSourcePreference(ResourceType type) => type switch {
        ResourceType.DataPack => CurseForgePreferredWeight,
        ResourceType.Mod or ResourceType.Modpack => ModrinthPreferredWeight,
        _ => DefaultPreferredWeight
    };

    private static double ComputeRelevanceScore(ResourceHit hit, string? query) {
        var score = hit.Source == ResourceSource.CurseForge
            ? GetSourcePreference(hit.Type) * 0.1
            : GetSourcePreference(hit.Type);

        if (string.IsNullOrWhiteSpace(query) || !ContainsCjk(query))
            return score;
        if (hit.Translation is { } translation &&
            translation.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            score += 10;

        return score;
    }

    private static bool ContainsCjk(string text) =>
        text.Any(character => character >= 0x2E80 && character <= 0x9FFF);

    private static string NormalizeName(string name) =>
        string.Concat(name.Where(character => !char.IsWhiteSpace(character) && character != '&'))
            .ToLowerInvariant();
}
