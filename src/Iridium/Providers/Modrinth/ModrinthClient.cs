using System.Text.Json;
using Iridium.Enums.Resources;
using Iridium.Extensions.Resources;
using Iridium.Helpers.Resources;
using Iridium.Interfaces.Resources;
using Iridium.Models.Modrinth;
using Iridium.Models.Resources;

namespace Iridium.Providers.Modrinth;


public sealed class ModrinthClient : IResourceClient {
    public const string ApiBase = "https://api.modrinth.com/v2";

    private readonly ResourceHttpClient _http;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public ResourceSource Source => ResourceSource.Modrinth;
    public ResourceApiOptions Options { get; }

    public ModrinthClient(ResourceApiOptions? options = null) {
        Options = options ?? new ResourceApiOptions();
        _http = new ResourceHttpClient(Options);
    }


    public Task<ModrinthSearchResult> SearchAsync(ResourceSearchOptions options,
        CancellationToken cancellationToken = default) =>
        GetJsonAsync<ModrinthSearchResult>(ModrinthRequestBuilder.BuildSearchUrl(options), cancellationToken);


    public async Task<IReadOnlyList<ModrinthSearchHit>> GetFeaturedAsync(CancellationToken cancellationToken = default) {
        var result = await GetJsonAsync<ModrinthSearchResult>($"{ApiBase}/search?limit=40", cancellationToken);
        return result.Hits;
    }


    public async Task<ModrinthProject?> GetProjectAsync(string projectId,
        CancellationToken cancellationToken = default) {
        var url = $"{ApiBase}/project/{Uri.EscapeDataString(projectId)}";
        return await GetJsonOrNullAsync<ModrinthProject>(url, cancellationToken);
    }


    public async Task<IReadOnlyList<ModrinthProject>> GetProjectsAsync(IEnumerable<string> projectIds,
        CancellationToken cancellationToken = default) {
        var ids = projectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
            return [];

        var url = $"{ApiBase}/projects?ids={Uri.EscapeDataString(JsonSerializer.Serialize(ids))}";
        return await GetJsonAsync<List<ModrinthProject>>(url, cancellationToken);
    }


    public async Task<IReadOnlyList<ModrinthVersion>> GetFilesAsync(string projectId,
        IReadOnlyList<string>? gameVersions = null,
        IReadOnlyList<ResourceLoaderType>? loaders = null,
        bool includeChangelog = false,
        CancellationToken cancellationToken = default) {
        var query = new List<string> { $"include_changelog={includeChangelog.ToString().ToLowerInvariant()}" };
        if (gameVersions is { Count: > 0 })
            query.Add($"game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(gameVersions))}");
        if (loaders is { Count: > 0 }) {
            var loaderSlugs = loaders.Select(loader => loader.ToModrinthLoader()).Where(slug => slug is not null);
            query.Add($"loaders={Uri.EscapeDataString(JsonSerializer.Serialize(loaderSlugs))}");
        }

        var url = $"{ApiBase}/project/{Uri.EscapeDataString(projectId)}/version?{string.Join("&", query)}";
        return await GetJsonAsync<List<ModrinthVersion>>(url, cancellationToken);
    }


    public async Task<ModrinthVersion?> GetVersionAsync(string versionId,
        CancellationToken cancellationToken = default) {
        var url = $"{ApiBase}/version/{Uri.EscapeDataString(versionId)}";
        return await GetJsonOrNullAsync<ModrinthVersion>(url, cancellationToken);
    }


    public async Task<ModrinthVersion?> GetVersionByHashAsync(string hash,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        var url = $"{ApiBase}/version_file/{Uri.EscapeDataString(hash)}?algorithm={ToModrinthAlgorithm(algorithm)}";
        return await GetJsonOrNullAsync<ModrinthVersion>(url, cancellationToken);
    }


    public async Task<IReadOnlyDictionary<string, ModrinthVersion?>> GetVersionsByHashesAsync(
        IEnumerable<string> hashes,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        var values = hashes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length == 0)
            return new Dictionary<string, ModrinthVersion?>();

        var json = await _http.PostJsonAsync($"{ApiBase}/version_files",
            new { hashes = values, algorithm = ToModrinthAlgorithm(algorithm) },
            cancellationToken: cancellationToken);
        return Deserialize<Dictionary<string, ModrinthVersion?>>(json);
    }


    public async Task<IReadOnlyDictionary<string, List<ModrinthVersion>>> CheckForUpdatesAsync(
        IEnumerable<string> hashes,
        string gameVersion,
        ResourceLoaderType loader,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        var values = hashes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length == 0)
            return new Dictionary<string, List<ModrinthVersion>>();

        var payload = new Dictionary<string, object?> {
            ["hashes"] = values,
            ["algorithm"] = ToModrinthAlgorithm(algorithm),
            ["loaders"] = loader.ToModrinthLoader() is { } slug ? new[] { slug } : Array.Empty<string>(),
            ["game_versions"] = new[] { gameVersion },
            ["version_types"] = new[] { "release", "beta", "alpha" }
        };

        var json = await _http.PostJsonAsync($"{ApiBase}/version_files/update", payload,
            cancellationToken: cancellationToken);
        return Deserialize<Dictionary<string, List<ModrinthVersion>>>(json);
    }


    public async Task<IReadOnlyList<ModrinthCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await GetJsonAsync<List<ModrinthCategory>>($"{ApiBase}/tag/category", cancellationToken);


    public async Task<IReadOnlyList<ModrinthLoader>> GetLoadersAsync(CancellationToken cancellationToken = default) =>
        await GetJsonAsync<List<ModrinthLoader>>($"{ApiBase}/tag/loader", cancellationToken);


    public async Task<IReadOnlyList<ModrinthGameVersion>> GetGameVersionsAsync(
        CancellationToken cancellationToken = default) =>
        await GetJsonAsync<List<ModrinthGameVersion>>($"{ApiBase}/tag/game_version", cancellationToken);

    async Task<IReadOnlyList<string>> IResourceClient.GetGameVersionsAsync(CancellationToken cancellationToken) {
        var versions = await GetGameVersionsAsync(cancellationToken);
        return versions.Select(version => version.Version).Where(version => version is not null)
            .Cast<string>().ToArray();
    }

    async Task<IReadOnlyList<ResourceCategory>> IResourceClient.GetCategoriesAsync(ResourceType type,
        CancellationToken cancellationToken) {
        var categories = await GetCategoriesAsync(cancellationToken);
        var projectType = type.ToModrinthProjectType();
        return categories
            .Where(category => string.Equals(category.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
            .Select(category => new ResourceCategory {
                Type = type,
                Name = category.Name ?? string.Empty,
                ModrinthSlug = category.Name
            })
            .ToArray();
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken) {
        var json = await _http.GetStringAsync(url, cancellationToken);
        return Deserialize<T>(json);
    }

    private async Task<T?> GetJsonOrNullAsync<T>(string url, CancellationToken cancellationToken) where T : class {
        var json = await _http.GetStringOrNullAsync(url, cancellationToken);
        return json is null ? null : Deserialize<T>(json);
    }

    private T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, _json) ?? throw new InvalidOperationException("无法解析 Modrinth 响应。");

    private static string ToModrinthAlgorithm(HashAlgorithm algorithm) => algorithm switch {
        HashAlgorithm.Sha1 => "sha1",
        HashAlgorithm.Sha512 => "sha512",
        _ => "sha1"
    };
}
