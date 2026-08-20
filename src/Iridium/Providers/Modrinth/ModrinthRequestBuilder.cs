using System.Text.Json;
using Iridium.Extensions.Resources;
using Iridium.Models.Resources;

namespace Iridium.Providers.Modrinth;

internal static class ModrinthRequestBuilder {
    public static string BuildSearchUrl(ResourceSearchOptions options) {
        var query = new List<string> {
            $"limit={Math.Clamp(options.PageSize, 1, 100)}",
            $"index={options.Sort.ToModrinthIndex()}",
            $"facets={Uri.EscapeDataString(BuildFacets(options))}"
        };

        var offset = Math.Max(0, (options.Page - 1) * options.PageSize);
        if (offset > 0)
            query.Add($"offset={offset}");
        if (!string.IsNullOrWhiteSpace(options.Query))
            query.Add($"query={Uri.EscapeDataString(options.Query)}");

        return $"{ModrinthClient.ApiBase}/search?{string.Join("&", query)}";
    }

    public static string BuildFacets(ResourceSearchOptions options) {
        var facets = new List<List<string>>();
        facets.Add([$"project_type:{options.Type.ToModrinthProjectType()}"]);

        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            facets.Add([$"versions:'{options.GameVersion}'"]);

        foreach (var tag in options.Tags) {
            if (!string.IsNullOrWhiteSpace(tag.ModrinthSlug))
                facets.Add([$"categories:'{tag.ModrinthSlug}'"]);
        }

        if (options.Loader.ToModrinthLoader() is { } loader)
            facets.Add([$"categories:'{loader}'"]);

        return JsonSerializer.Serialize(facets);
    }
}
