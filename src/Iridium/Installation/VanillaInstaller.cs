using System.Text.Json;
using Flurl.Http;
using Iridium.Download;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Download;
using Iridium.Models.Installation;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Minecraft;
using Iridium.Providers.Minecraft;

namespace Iridium.Installation;

public sealed class VanillaInstaller : InstallerBase {
    private const string VersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private readonly DirectoryInfo _root;
    private readonly DownloadSource _source;
    private readonly MinecraftProvider _provider;
    
    private readonly int _maxConcurrency;
    
    public VersionManifestEntry? Entry { get; private set; }

    // public override string MinecraftFolder => _root.FullName;

    public VanillaInstaller(DirectoryInfo root, DownloadSource source, int maxConcurrency = 32) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);

        _root = root;
        _source = source;
        _maxConcurrency = maxConcurrency;
        _provider = new MinecraftProvider(root);
    }

    public static async Task<IEnumerable<VersionManifestEntry>?> EnumerableMinecraftAsync(CancellationToken cancellationToken = default) {
        await using var stream = await VersionManifestUrl
            .GetStreamAsync(HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var versions = doc.RootElement.GetProperty("versions");

        var result = versions.Deserialize<IEnumerable<VersionManifestEntry>>(
            VersionManifestEntryContext.Default.IEnumerableVersionManifestEntry);
        
        return result;
    }

    public override async Task<DownloadResponse> InstallAsync(VersionManifestEntry id, IMinecraftLayout layout, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(layout);

        var steps = new[] { "Download version JSON", "Parse version JSON", "Download asset index", "Download game resources" };

        try {
            ReportProgress(steps, 0, 0, 0);
            Entry = id;

            var versionJsonPath = await DownloadVersionJsonAsync(layout, cancellationToken)
                .ConfigureAwait(false);
            
            var entry = await _provider.GetMinecraftAsync(Entry!.Id, cancellationToken);
            
            await CompleteMinecraftDependenciesAsync(entry, layout, cancellationToken).ConfigureAwait(false);

            ReportCompleted(true);
            return new DownloadResponse { SuccessCount = 1, FailCount = 0 };
        } catch (OperationCanceledException) {
            ReportCompleted(false);
            return new DownloadResponse { SuccessCount = 0, FailCount = 0 };
        } catch (Exception ex) {
            ReportCompleted(false, ex);
            return new DownloadResponse { SuccessCount = 0, FailCount = 1, Exceptions = [ex] };
        }
    }
    
    private async Task<FileInfo> DownloadVersionJsonAsync(IMinecraftLayout layout, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(["Download version JSON", "Parse version JSON", "Download asset index", "Download game resources"],
            0, 0, 0.15);

        await using var jsonStream = await Entry!.Url
            .GetStreamAsync(HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);


        var jsonPath = new FileInfo(Path.Combine(_root.FullName, layout.GetVersionJsonPath(Entry.Id)));
        if (!jsonPath.Directory!.Exists)
            jsonPath.Directory.Create();

        await using var output = jsonPath.OpenWrite();
        await jsonStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        ReportProgress(["Download version JSON", "Parse version JSON", "Download asset index", "Download game resources"], 0, 0, 0.3);
        return jsonPath;
    }

    private async Task CompleteMinecraftDependenciesAsync(MinecraftEntry entry, IMinecraftLayout layout, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(["Download version JSON", "Parse version JSON", "Download asset index", "Download game resources"], 3, 3, 0.5);

        using var resourceDownloader = new ResourceDownloader(_source, _maxConcurrency, layout);
        resourceDownloader.ProgressChanged += (_, args) => {
            var stepProgress = args.TotalCount > 0 ? 0.5 + (args.CompletedCount / (double)args.TotalCount) * 0.5 : 0.5;
            ReportProgress(["Download version JSON", "Parse version JSON", "Download asset index", "Download game resources"], 3, 3, stepProgress);
        };

        var result = await resourceDownloader.DownloadAsync(entry, cancellationToken).ConfigureAwait(false);

        if (result.FailCount > 0)
            throw new InvalidOperationException("Some dependent files encountered errors during download. FailCount: " + result.FailCount);
    }
}