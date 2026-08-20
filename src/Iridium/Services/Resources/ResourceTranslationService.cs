using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl.Http;
using Iridium.Enums.Resources;

namespace Iridium.Services.Resources;


public sealed class ResourceTranslationService {
    private const int BatchSize = 50;
    private const int MaximumConcurrentRequests = 4;

    private readonly string _apiRoot;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly string _userAgent;

    public ResourceTranslationService(string apiRoot = "https://mod.mcimirror.top/translate",
        string userAgent = "Iridium") {
        _apiRoot = apiRoot.TrimEnd('/');
        _userAgent = userAgent;
    }


    public async Task<IReadOnlyDictionary<string, string>> GetTranslationsAsync(
        ResourceSource source, IEnumerable<string> projectIds, bool forceRefresh = false,
        CancellationToken cancellationToken = default) {
        var requested = projectIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        var results = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var id in requested) {
            if (!forceRefresh && _cache.TryGetValue(GetCacheKey(source, id), out var translated)) {
                if (!string.IsNullOrWhiteSpace(translated))
                    results[id] = translated;
            } else {
                missing.Add(id);
            }
        }

        using var semaphore = new SemaphoreSlim(MaximumConcurrentRequests);
        await Task.WhenAll(missing.Chunk(BatchSize).Select(async batch => {
            await semaphore.WaitAsync(cancellationToken);
            try {
                var translations = await FetchBatchAsync(source, batch, cancellationToken);
                if (translations is null)
                    return;

                foreach (var id in batch)
                    _cache.TryAdd(GetCacheKey(source, id), string.Empty);
                foreach (var translation in translations) {
                    if (string.IsNullOrWhiteSpace(translation.ProjectId) ||
                        string.IsNullOrWhiteSpace(translation.Translated))
                        continue;
                    _cache[GetCacheKey(source, translation.ProjectId)] = translation.Translated;
                    results[translation.ProjectId] = translation.Translated;
                }
            } finally {
                semaphore.Release();
            }
        }));

        return results;
    }

    private async Task<IReadOnlyList<ProjectTranslation>?> FetchBatchAsync(ResourceSource source,
        string[] projectIds, CancellationToken cancellationToken) {
        try {
            if (source == ResourceSource.CurseForge) {
                var ids = projectIds.Select(id => int.TryParse(id, out var value) ? (int?)value : null)
                    .Where(id => id.HasValue).Select(id => id!.Value).ToArray();
                if (ids.Length == 0)
                    return [];

                var response = await $"{_apiRoot}/curseforge"
                    .WithHeader("Accept", "application/json")
                    .WithHeader("User-Agent", _userAgent)
                    .WithTimeout(TimeSpan.FromSeconds(8))
                    .PostJsonAsync(new { modids = ids }, cancellationToken: cancellationToken)
                    .ReceiveJson<List<CurseForgeTranslation>>();
                return response.Select(item => new ProjectTranslation(item.ModId.ToString(), item.Translated))
                    .ToArray();
            }

            var modrinthResponse = await $"{_apiRoot}/modrinth"
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", _userAgent)
                .WithTimeout(TimeSpan.FromSeconds(8))
                .PostJsonAsync(new { project_ids = projectIds }, cancellationToken: cancellationToken)
                .ReceiveJson<List<ModrinthTranslation>>();
            return modrinthResponse.Select(item => new ProjectTranslation(item.ProjectId, item.Translated)).ToArray();
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (FlurlHttpException) {
            return null;
        } catch (JsonException) {
            return null;
        }
    }

    private static string GetCacheKey(ResourceSource source, string projectId) => $"{source}:{projectId}";

    private sealed record ProjectTranslation(string ProjectId, string? Translated);

    private sealed record CurseForgeTranslation(
        [property: JsonPropertyName("modid")] int ModId,
        [property: JsonPropertyName("translated")] string? Translated);

    private sealed record ModrinthTranslation(
        [property: JsonPropertyName("project_id")] string ProjectId,
        [property: JsonPropertyName("translated")] string? Translated);
}
