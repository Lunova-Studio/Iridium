using System.Text.Json;
using Iridium.Enums.Resources;
using Iridium.Extensions.Resources;
using Iridium.Models.Resources;

namespace Iridium.Providers.CurseForge;

internal static class CurseForgeRequestBuilder {
    public const int MinecraftGameId = 432;

    public static string BuildSearchUrl(ResourceSearchOptions options) {
        var query = new List<string> {
            $"gameId={MinecraftGameId}",
            $"sortOrder={(options.SortOrder == SortOrder.Asc ? "asc" : "desc")}",
            $"sortField={options.Sort.ToCurseForgeSortField()}",
            $"index={Math.Max(0, (options.Page - 1) * options.PageSize)}",
            $"pageSize={Math.Clamp(options.PageSize, 1, 50)}"
        };

        if (options.Type.ToCurseForgeClassId() is { } classId)
            query.Add($"classId={classId}");

        var curseForgeTags = options.Tags.Select(tag => tag.CurseForgeId).Where(id => id.HasValue)
            .Select(id => id!.Value).Distinct().Take(10).ToArray();
        if (curseForgeTags.Length == 1)
            query.Add($"categoryId={curseForgeTags[0]}");
        else if (curseForgeTags.Length > 1)
            query.Add($"categoryIds={Uri.EscapeDataString(JsonSerializer.Serialize(curseForgeTags))}");

        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            query.Add($"gameVersion={Uri.EscapeDataString(options.GameVersion)}");
        if (options.Loader.ToCurseForgeLoaderType() is { } loader)
            query.Add($"modLoaderType={loader}");
        if (!string.IsNullOrWhiteSpace(options.Query))
            query.Add($"searchFilter={Uri.EscapeDataString(options.Query)}");

        return $"{CurseForgeClient.ApiBase}/mods/search?{string.Join("&", query)}";
    }
}
