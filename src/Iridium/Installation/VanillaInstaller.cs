using System.Text.Json;
using Flurl.Http;
using Iridium.Download;
using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Installation;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;
using Iridium.Parsers.Minecraft;

namespace Iridium.Installation;

public sealed class VanillaInstaller : InstallerBase {
    private const string VersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    private static readonly string[] InstallSteps = [
        "Download version JSON",
        "Parse version JSON",
        "Download asset index",
        "Download game resources"];

    private readonly DirectoryInfo _root;
    private readonly DownloadSource _source;
    private readonly IMinecraftLayout _layout;
    private readonly int _maxConcurrency;

    public VanillaInstaller(DirectoryInfo root, DownloadSource source,
        MinecraftFormat? format = null, IMinecraftLayoutFactory? factory = null, int maxConcurrency = 32) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);

        _root = root;
        _source = source;
        _maxConcurrency = maxConcurrency;
        _layout = (factory ?? new DefaultMinecraftLayoutFactory()).Create(format ?? MinecraftFormat.Standard);
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

    public override async Task<MinecraftInstallResult> InstallAsync(VersionManifestEntry id, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(id);

        try {
            ReportProgress(InstallSteps, 0, 0, 0);

            var instancePath = Path.Combine(_root.FullName, _layout.GetInstanceDirectory(id.Id));
            var seed = new MinecraftEntry { Id = id.Id, InstancePath = instancePath };

            var versionJsonPath = await DownloadVersionJsonAsync(id.Url, _layout.GetVersionJsonPath(seed), cancellationToken)
                .ConfigureAwait(false);

            var entry = await ParseVersionAsync(versionJsonPath, seed, cancellationToken)
                .ConfigureAwait(false);

            await CompleteDependenciesAsync(entry, cancellationToken).ConfigureAwait(false);

            ReportCompleted(true);
            return new MinecraftInstallResult {
                Entry = entry,
                VersionJsonPath = versionJsonPath.FullName,
                ClientJarPath = _layout.GetVersionJarPath(entry)
            };
        } catch (OperationCanceledException) {
            ReportCompleted(false);
            throw;
        } catch (Exception ex) {
            ReportCompleted(false, ex);
            throw;
        }
    }

    private async Task<FileInfo> DownloadVersionJsonAsync(string url, string jsonPath, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallSteps, 0, 0, 0.15);

        await using var jsonStream = await url
            .GetStreamAsync(HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        var jsonFile = new FileInfo(jsonPath);
        if (!jsonFile.Directory!.Exists)
            jsonFile.Directory.Create();

        // FileMode.Create truncates an existing file; OpenWrite() would leave stale
        // trailing bytes behind on re-download and corrupt the JSON.
        await using var output = new FileStream(
            jsonFile.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await jsonStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        ReportProgress(InstallSteps, 0, 0, 0.3);
        return jsonFile;
    }

    private async Task<MinecraftEntry> ParseVersionAsync(FileInfo versionJsonPath, MinecraftEntry seed, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallSteps, 1, 1, 0.4);

        await using var stream = new FileStream(
            versionJsonPath.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var entry = VersionJsonParser.MapEntry(document.RootElement, seed.Id);
        return entry with {
            InstancePath = seed.InstancePath,
            MinecraftVersion = seed.Id,
            Format = _layout.Format
        };
    }

    private async Task CompleteDependenciesAsync(MinecraftEntry entry, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallSteps, 3, 3, 0.5);

        using var resourceDownloader = new ResourceDownloader(_source, _maxConcurrency, layout: _layout);
        resourceDownloader.ProgressChanged += (_, args) => {
            var stepProgress = args.TotalCount > 0 ? 0.5 + args.CompletedCount / (double)args.TotalCount * 0.5 : 0.5;
            ReportProgress(InstallSteps, 3, 3, stepProgress);
        };

        var result = await resourceDownloader.DownloadAsync(entry, cancellationToken).ConfigureAwait(false);

        if (result.FailCount > 0)
            throw new InvalidOperationException("Some dependent files encountered errors during download. FailCount: " + result.FailCount);
    }
}
