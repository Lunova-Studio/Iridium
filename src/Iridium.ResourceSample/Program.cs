using Iridium.Enums.Resources;
using Iridium.Enums.ResourceCategories;
using Iridium.Extensions.Resources;
using Iridium.Helpers.Resources;
using Iridium.Models.Resources;
using Iridium.Providers.CurseForge;
using Iridium.Providers.Modrinth;
using Iridium.Services.Resources;

var curseForgeKey = Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY");
var options = new ResourceApiOptions {
    CurseForgeApiKey = string.IsNullOrWhiteSpace(curseForgeKey) ? null : curseForgeKey,
    Mode = ResourceDownloadMode.MirrorPreferred,
    Mirrors = [new McimResourceMirror()]
};

var modrinth = new ModrinthClient(options);
var curseForge = new CurseForgeClient(options);
var service = new ResourceSearchService(modrinth, curseForge);

if (string.IsNullOrWhiteSpace(curseForgeKey))
    Console.WriteLine("未设置 CURSEFORGE_API_KEY，CurseForge 相关演示会跳过。\n");

await DemoCustomMirrorAsync();
await DemoSearchAsync(modrinth, service);
await DemoFileGroupingAsync(modrinth);
await DemoLocalFileLookupAsync(modrinth, curseForge);
await DemoStaticCategoriesAsync();

static async Task DemoCustomMirrorAsync() {
    Console.WriteLine("自定义镜像源");
    var mirror = CustomResourceMirror.CreateFromMap("my-mirror", new Dictionary<string, string> {
        ["api.modrinth.com"] = "https://mirror.example.com/modrinth"
    });
    Console.WriteLine(mirror.TryRewrite("https://api.modrinth.com/v2/search"));
    Console.WriteLine();
}

static async Task DemoSearchAsync(ModrinthClient modrinth, ResourceSearchService service) {
    var query = new ResourceSearchOptions {
        Source = ResourceSource.Modrinth,
        Type = ResourceType.Mod,
        Query = "sodium",
        GameVersion = "1.20.1",
        Loader = ResourceLoaderType.Fabric,
        Sort = ResourceSort.Relevance,
        PageSize = 6
    };

    Console.WriteLine("搜索原始数据");
    var raw = await modrinth.SearchAsync(query);
    foreach (var hit in raw.Hits)
        Console.WriteLine($"{hit.Title}, 下载 {hit.Downloads:N0}");
    Console.WriteLine($"共 {raw.TotalHits} 条\n");

    Console.WriteLine("处理后数据");
    var page = await service.SearchAsync(query);
    foreach (var hit in page.Items)
        Console.WriteLine($"{hit.Source}: {hit.Title}, 下载 {hit.Downloads:N0}");
    Console.WriteLine();

    Console.WriteLine("排序对比");
    var mapped = raw.Hits.Select(hit => hit.ToResourceHit(query.Type)).ToArray();
    Console.WriteLine("原始顺序");
    foreach (var hit in mapped)
        Console.WriteLine($"{hit.Title} ({hit.Downloads:N0})");
    var sorted = ResourceMerger.MergeAndSort(mapped, query.Type, ResourceSort.Newest, query.Query);
    Console.WriteLine("按发布日期排序后");
    foreach (var hit in sorted)
        Console.WriteLine($"{hit.Title} ({hit.DateCreated:yyyy-MM-dd})");
    Console.WriteLine();

    Console.WriteLine("翻译");
    var translated = await service.TranslateAsync(page.Items);
    foreach (var hit in translated)
        Console.WriteLine($"{hit.Title}: {hit.Translation ?? hit.Summary}");
    Console.WriteLine();
}

static async Task DemoFileGroupingAsync(ModrinthClient modrinth) {
    var project = await modrinth.GetProjectAsync("sodium");
    if (project is null) {
        Console.WriteLine("项目 sodium 未找到");
        return;
    }

    var files = await modrinth.GetFilesAsync(project.Id!);

    Console.WriteLine("原始文件列表");
    foreach (var file in files.Take(8))
        Console.WriteLine($"{file.Name} | {file.VersionNumber} | 版本: {string.Join(",", file.GameVersions)} | 加载器: {string.Join(",", file.Loaders)}");
    Console.WriteLine($"共 {files.Count} 个版本\n");

    Console.WriteLine("处理后分组");
    var mapped = files.Select(file => file.ToResourceFile()).ToArray();
    var grouping = ResourceFileGrouper.Group(mapped);
    foreach (var version in grouping.Versions.Take(4)) {
        Console.WriteLine($"版本 {version.GameVersion}");
        foreach (var loader in version.Loaders)
            Console.WriteLine($"{loader.Loader}: {loader.Files.Count} 个文件, 推荐 {loader.Recommended?.Name}");
    }
    Console.WriteLine();

    Console.WriteLine("筛选");
    var filtered = ResourceFileGrouper.Filter(mapped, "1.20.1", ResourceLoaderType.Fabric,
        ReleaseType.Release | ReleaseType.Beta);
    foreach (var file in filtered.Take(5))
        Console.WriteLine(file.Name);
    Console.WriteLine($"共 {filtered.Count} 个\n");
}

async Task DemoLocalFileLookupAsync(ModrinthClient modrinth, CurseForgeClient curseForge) {
    Console.WriteLine("本地文件哈希反查");
    var path = @"C:\Users\84067\AppData\Roaming\cc.tiouo.portal.minecraft\instances\Fabulously Optimized\mods\iris-fabric-1.11.2+mc26.2.jar";
    if (!File.Exists(path)) {
        Console.WriteLine($"文件不存在: {path}");
        return;
    }

    var bytes = await File.ReadAllBytesAsync(path);
    var sha1 = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes)).ToLowerInvariant();
    var fingerprint = CurseForgeFingerprintHelper.Compute(bytes);
    Console.WriteLine($"文件: {Path.GetFileName(path)}");
    Console.WriteLine($"SHA1: {sha1}");
    Console.WriteLine($"CF 指纹: {fingerprint}");

    var version = await modrinth.GetVersionByHashAsync(sha1);
    if (version is not null) {
        var project = await modrinth.GetProjectAsync(version.ProjectId!);
        Console.WriteLine($"Modrinth 命中: {version.Name}, 项目: {project?.Title}");
    } else {
        Console.WriteLine("Modrinth 未命中该 SHA1");
    }

    if (!string.IsNullOrWhiteSpace(curseForge.Options.CurseForgeApiKey)) {
        var result = await curseForge.GetFilesByFingerprintsAsync([fingerprint]);
        var match = result.Data?.ExactMatches.FirstOrDefault();
        if (match is not null) {
            Console.WriteLine($"CurseForge 命中: {match.File?.FileName}");
            if (match.File is { ModId: { } modId }) {
                var project = await curseForge.GetProjectAsync(modId);
                Console.WriteLine($"CF 项目: {project?.Name} (id={modId})");
            }
        } else {
            Console.WriteLine("CurseForge 未命中该指纹");
        }
    } else {
        Console.WriteLine("CurseForge 跳过（未设置 API Key）");
    }

    Console.WriteLine();
}

static async Task DemoStaticCategoriesAsync() {
    Console.WriteLine("静态标签目录");
    var tags = new[] {
        ModCategory.Optimization.ToResourceCategory(),
        ModCategory.Magic.ToResourceCategory(),
        ResourcePackCategory.Resolution32x.ToResourceCategory()
    };
    foreach (var tag in tags)
        Console.WriteLine($"{tag.DisplayName}, CurseForge ID {tag.CurseForgeId}, Modrinth {tag.ModrinthSlug}");
    Console.WriteLine();
}
