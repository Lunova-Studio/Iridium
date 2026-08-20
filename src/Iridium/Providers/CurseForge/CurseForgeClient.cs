using System.Text.Json;
using Iridium.Enums.Resources;
using Iridium.Extensions.Resources;
using Iridium.Helpers.Resources;
using Iridium.Interfaces.Resources;
using Iridium.Models.CurseForge;
using Iridium.Models.Resources;

namespace Iridium.Providers.CurseForge;


public sealed class CurseForgeClient : IResourceClient {
    public const string ApiBase = "https://api.curseforge.com/v1";

    private readonly ResourceHttpClient _http;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public ResourceSource Source => ResourceSource.CurseForge;
    public ResourceApiOptions Options { get; }

    public CurseForgeClient(ResourceApiOptions? options = null) {
        Options = options ?? new ResourceApiOptions();
        _http = new ResourceHttpClient(Options);
    }


    public async Task<CurseForgeSearchResult> SearchAsync(ResourceSearchOptions options,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var url = CurseForgeRequestBuilder.BuildSearchUrl(options);
        var json = await _http.GetStringAsync(url, cancellationToken);
        return Deserialize<CurseForgePagedResponse<List<CurseForgeProject>>>(json) is { } response
            ? new CurseForgeSearchResult {
                Items = response.Data ?? [],
                Pagination = response.Pagination
            }
            : new CurseForgeSearchResult();
    }


    public async Task<IReadOnlyList<CurseForgeProject>> GetFeaturedAsync(CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.PostJsonAsync($"{ApiBase}/mods/featured",
            new { gameId = CurseForgeRequestBuilder.MinecraftGameId, excludedModIds = new[] { 0 } },
            cancellationToken: cancellationToken);
        var result = Deserialize<CurseForgeResponse<CurseForgeFeaturedResult>>(json);
        if (result?.Data is null)
            return [];

        var seen = new HashSet<long>();
        var projects = new List<CurseForgeProject>();
        foreach (var project in result.Data.Popular.Concat(result.Data.Featured)) {
            if (seen.Add(project.Id))
                projects.Add(project);
        }

        return projects;
    }


    public async Task<CurseForgeProject?> GetProjectAsync(long modId, CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.GetStringOrNullAsync($"{ApiBase}/mods/{modId}", cancellationToken);
        return json is null ? null : Deserialize<CurseForgeResponse<CurseForgeProject>>(json)?.Data;
    }


    public async Task<IReadOnlyList<CurseForgeProject>> GetProjectsAsync(IEnumerable<long> modIds,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var ids = modIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var projects = new List<CurseForgeProject>();
        foreach (var batch in ids.Chunk(50)) {
            var json = await _http.PostJsonAsync($"{ApiBase}/mods", new { modIds = batch },
                cancellationToken: cancellationToken);
            var data = Deserialize<CurseForgeResponse<List<CurseForgeProject>>>(json)?.Data;
            if (data is not null)
                projects.AddRange(data);
        }

        return projects;
    }


    public async Task<IReadOnlyList<CurseForgeFile>> GetFilesAsync(long modId,
        string? gameVersion = null,
        ResourceLoaderType loader = ResourceLoaderType.Any,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        const int pageSize = 20;
        const int maximumConcurrentRequests = 16;

        async Task<(List<CurseForgeFile> Files, int TotalCount)> GetPageAsync(int index) {
            var query = new List<string> { $"index={index}", $"pageSize={pageSize}" };
            if (!string.IsNullOrWhiteSpace(gameVersion))
                query.Add($"gameVersion={Uri.EscapeDataString(gameVersion)}");
            if (loader.ToCurseForgeLoaderType() is { } loaderType)
                query.Add($"modLoaderType={loaderType}");

            var json = await _http.GetStringAsync($"{ApiBase}/mods/{modId}/files?{string.Join("&", query)}",
                cancellationToken);
            var response = Deserialize<CurseForgePagedResponse<List<CurseForgeFile>>>(json);
            return (response?.Data ?? [], response?.Pagination?.TotalCount ?? 0);
        }

        var firstPage = await GetPageAsync(0);
        var files = new List<CurseForgeFile>(firstPage.Files);
        var totalCount = firstPage.TotalCount;
        if (files.Count >= totalCount || files.Count < pageSize)
            return files;

        var pageCount = (totalCount + pageSize - 1) / pageSize;
        foreach (var batch in Enumerable.Range(1, pageCount - 1).Chunk(maximumConcurrentRequests)) {
            var results = await Task.WhenAll(batch.Select(index => GetPageAsync(index)));
            foreach (var result in results)
                files.AddRange(result.Files);
        }

        return files.Take(totalCount).ToList();
    }


    public async Task<CurseForgeFile?> GetFileAsync(long modId, long fileId,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.GetStringOrNullAsync($"{ApiBase}/mods/{modId}/files/{fileId}", cancellationToken);
        return json is null ? null : Deserialize<CurseForgeResponse<CurseForgeFile>>(json)?.Data;
    }


    public async Task<IReadOnlyList<CurseForgeFile>> GetFilesByFileIdsAsync(IEnumerable<long> fileIds,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var ids = fileIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var files = new List<CurseForgeFile>();
        foreach (var batch in ids.Chunk(50)) {
            var json = await _http.PostJsonAsync($"{ApiBase}/mods/files", new { fileIds = batch },
                cancellationToken: cancellationToken);
            var data = Deserialize<CurseForgeResponse<List<CurseForgeFile>>>(json)?.Data;
            if (data is not null)
                files.AddRange(data);
        }

        return files;
    }


    public async Task<CurseForgeFingerprintResult> GetFilesByFingerprintsAsync(IEnumerable<uint> fingerprints,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var values = fingerprints.Distinct().ToArray();
        if (values.Length == 0)
            return new CurseForgeFingerprintResult { Data = new CurseForgeFingerprintData() };

        var json = await _http.PostJsonAsync($"{ApiBase}/fingerprints/{CurseForgeRequestBuilder.MinecraftGameId}",
            new { fingerprints = values }, cancellationToken: cancellationToken);
        return Deserialize<CurseForgeFingerprintResult>(json) ?? new CurseForgeFingerprintResult();
    }


    public async Task<string?> GetDownloadUrlAsync(long modId, long fileId,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.GetStringOrNullAsync($"{ApiBase}/mods/{modId}/files/{fileId}/download-url",
            cancellationToken);
        return json is null ? null : Deserialize<CurseForgeResponse<string>>(json)?.Data;
    }


    public async Task<string?> GetChangelogAsync(long modId, long fileId,
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.GetStringOrNullAsync($"{ApiBase}/mods/{modId}/files/{fileId}/changelog",
            cancellationToken);
        return json is null ? null : Deserialize<CurseForgeResponse<string>>(json)?.Data;
    }


    public async Task<IReadOnlyList<CurseForgeCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.GetStringAsync(
            $"{ApiBase}/categories?gameId={CurseForgeRequestBuilder.MinecraftGameId}", cancellationToken);
        return Deserialize<CurseForgeResponse<List<CurseForgeCategory>>>(json)?.Data ?? [];
    }


    public async Task<IReadOnlyList<CurseForgeGameVersion>> GetGameVersionsAsync(
        CancellationToken cancellationToken = default) {
        CheckApiKey();
        var json = await _http.GetStringAsync(
            $"{ApiBase}/games/{CurseForgeRequestBuilder.MinecraftGameId}/versions", cancellationToken);
        return Deserialize<CurseForgeResponse<List<CurseForgeGameVersion>>>(json)?.Data ?? [];
    }

    async Task<IReadOnlyList<string>> IResourceClient.GetGameVersionsAsync(CancellationToken cancellationToken) {
        var versions = await GetGameVersionsAsync(cancellationToken);
        return versions.Select(version => version.VersionString).Where(version => version is not null)
            .Cast<string>().ToArray();
    }

    async Task<IReadOnlyList<ResourceCategory>> IResourceClient.GetCategoriesAsync(ResourceType type,
        CancellationToken cancellationToken) {
        var categories = await GetCategoriesAsync(cancellationToken);
        var classId = type.ToCurseForgeClassId();
        return categories
            .Where(category => category.ClassId == classId || category.Id == classId)
            .Select(category => new ResourceCategory {
                Type = type,
                Name = category.Slug ?? string.Empty,
                DisplayName = category.Name,
                CurseForgeId = category.Id
            })
            .ToArray();
    }

    private void CheckApiKey() {
        if (string.IsNullOrWhiteSpace(Options.CurseForgeApiKey))
            throw new InvalidOperationException("CurseForge API key is not set. 请通过 ResourceApiOptions.CurseForgeApiKey 提供。");
    }

    private T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _json);
}


public sealed record CurseForgeSearchResult {
    public IReadOnlyList<CurseForgeProject> Items { get; init; } = [];
    public CurseForgePagination? Pagination { get; init; }
}
