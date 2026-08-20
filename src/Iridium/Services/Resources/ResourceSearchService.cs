using Iridium.Enums.Resources;
using Iridium.Extensions.Resources;
using Iridium.Interfaces.Resources;
using Iridium.Models.CurseForge;
using Iridium.Models.Modrinth;
using Iridium.Models.Resources;
using Iridium.Providers.CurseForge;
using Iridium.Providers.Modrinth;

namespace Iridium.Services.Resources;


public sealed class ResourceSearchService {
    private readonly ModrinthClient _modrinth;
    private readonly CurseForgeClient _curseForge;
    private readonly ResourceTranslationService _translations;

    public ResourceSearchService(ModrinthClient? modrinth = null, CurseForgeClient? curseForge = null,
        ResourceTranslationService? translations = null) {
        _modrinth = modrinth ?? new ModrinthClient();
        _curseForge = curseForge ?? new CurseForgeClient();
        _translations = translations ?? new ResourceTranslationService();
    }

    public ModrinthClient Modrinth => _modrinth;
    public CurseForgeClient CurseForge => _curseForge;


    public async Task<ResourceSearchPage<ResourceHit>> SearchAsync(ResourceSearchOptions options,
        CancellationToken cancellationToken = default) {
        var modrinthEnabled = options.Source.HasFlag(ResourceSource.Modrinth) && options.Type != ResourceType.World;
        var curseForgeEnabled = options.Source.HasFlag(ResourceSource.CurseForge);

        Task<ModrinthSearchResult>? modrinthTask = modrinthEnabled
            ? _modrinth.SearchAsync(options, cancellationToken)
            : null;
        Task<CurseForgeSearchResult>? curseForgeTask = curseForgeEnabled
            ? _curseForge.SearchAsync(options, cancellationToken)
            : null;

        var tasks = new List<Task>(2);
        if (modrinthTask is not null) tasks.Add(modrinthTask);
        if (curseForgeTask is not null) tasks.Add(curseForgeTask);
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);

        var hits = new List<ResourceHit>();
        var totalCount = 0;

        if (modrinthTask is not null) {
            var result = await modrinthTask;
            totalCount += (int)result.TotalHits;
            hits.AddRange(result.Hits.Select(hit => hit.ToResourceHit(options.Type)));
        }

        if (curseForgeTask is not null) {
            var result = await curseForgeTask;
            totalCount += result.Pagination?.TotalCount ?? 0;
            hits.AddRange(result.Items.Select(project => project.ToResourceHit(options.Type)));
        }

        var merged = ResourceMerger.MergeAndSort(hits, options.Type, options.Sort, options.Query);
        return new ResourceSearchPage<ResourceHit>(merged, totalCount, options.Page, options.PageSize);
    }

    public async Task<ResourceHit> TranslateAsync(ResourceHit hit, CancellationToken cancellationToken = default) {
        var translated = await TranslateAsync([hit], cancellationToken);
        return translated.Count > 0 ? translated[0] : hit;
    }

    public async Task<IReadOnlyList<ResourceHit>> TranslateAsync(IEnumerable<ResourceHit> hits,
        CancellationToken cancellationToken = default) {
        var list = hits as IReadOnlyList<ResourceHit> ?? hits.ToArray();
        if (list.Count == 0)
            return list;
        return await list.EnrichWithTranslationsAsync(_translations, cancellationToken);
    }


    public Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default) =>
        ((IResourceClient)_modrinth).GetGameVersionsAsync(cancellationToken);


    public Task<IReadOnlyList<ResourceCategory>> GetCategoriesAsync(ResourceType type,
        ResourceSource source = ResourceSource.All, bool includeStatic = true,
        CancellationToken cancellationToken = default) =>
        new ResourceCategoryService(_modrinth, _curseForge)
            .GetCategoriesAsync(type, source, includeStatic, cancellationToken);
}
