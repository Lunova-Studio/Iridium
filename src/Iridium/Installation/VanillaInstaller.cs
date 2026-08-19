using System.Text.Json;
using Flurl.Http;
using Iridium.Download;
using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Download;
using Iridium.Models.Installation;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;
using Iridium.Parsers.Minecraft;

namespace Iridium.Installation;

public sealed class VanillaInstaller : InstallerBase {
    private const string VersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private const int DownloadVersionStep = 0;
    private const int ParseVersionStep = 1;
    private const int DownloadResourcesStep = 2;

    private readonly DirectoryInfo _root;
    private readonly DownloadSource _source;
    private readonly IMinecraftLayout _layout;
    private readonly VersionManifestEntry _versionManifestEntry;

    private readonly int _maxConcurrency;

    protected override StepInfo[] Steps { get; } = [
        new("Download version JSON", 0.05d),
        new("Parse version JSON", 0.4d),
        new("Download game resources", 0.40d)
    ];

    public VanillaInstaller(
        DirectoryInfo root,
        VersionManifestEntry entry,
        DownloadSource? source = null,
        MinecraftFormat? format = null,
        IMinecraftLayoutFactory? factory = null,
        int maxConcurrency = 32) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(entry);
        
        _root = root;
        _versionManifestEntry = entry;
        _maxConcurrency = Math.Max(1, maxConcurrency);

        _source = source ?? DownloadSource.Official;
        _layout = (factory ?? new DefaultMinecraftLayoutFactory())
            .Create(format ?? MinecraftFormat.Standard);

        InitializeProgress();
    }

    public static async Task<IEnumerable<VersionManifestEntry>?> EnumerableMinecraftAsync(CancellationToken cancellationToken = default) {
        await using var stream = await VersionManifestUrl
            .GetStreamAsync(HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return document.RootElement
            .GetProperty("versions")
            .Deserialize<IEnumerable<VersionManifestEntry>>(
                VersionManifestEntryContext.Default.IEnumerableVersionManifestEntry);
    }

    public override async Task<MinecraftInstallResult> InstallAsync(CancellationToken cancellationToken = default) {
        try {
            var instancePath = Path.Combine(_root.FullName, _layout.GetInstanceDirectory(_versionManifestEntry.Id));
            var seed = new MinecraftEntry {
                Id = _versionManifestEntry.Id,
                InstancePath = instancePath
            };

            var versionJsonPath = await DownloadVersionJsonAsync(_versionManifestEntry.Url, _layout
                    .GetVersionJsonPath(seed), 
                    cancellationToken)
                .ConfigureAwait(false);

            CompleteStep(DownloadVersionStep);

            var entry = await ParseVersionAsync(seed, versionJsonPath, cancellationToken)
                .ConfigureAwait(false);

            CompleteStep(ParseVersionStep);

            await CompleteDependenciesAsync(entry, cancellationToken)
                .ConfigureAwait(false);

            // Deploy the un-hashed asset layout so that pre-1.6 versions get their
            // resources/ folder (sounds) and 1.6+ versions get assets/virtual/<id>.
            await new AssetsReconstructor(_layout)
                .ReconstructAsync(entry, _layout.GetGameDirectory(entry), cancellationToken)
                .ConfigureAwait(false);

            CompleteStep(DownloadResourcesStep);
            ReportCompleted(true);

            return new MinecraftInstallResult {
                Entry = entry,
                VersionJsonPath = versionJsonPath.FullName,
                ClientJarPath = _layout.GetVersionJarPath(entry)
            };
        }
        catch (OperationCanceledException) {
            ReportCompleted(false);
            throw;
        }
        catch (Exception exception) {
            ReportCompleted(false, exception);
            throw;
        }
    }

    private async Task<FileInfo> DownloadVersionJsonAsync(string url, string jsonPath, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateStep(DownloadVersionStep, 0, 1);

        await using var input = await url
            .GetStreamAsync(HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        var jsonFile = new FileInfo(jsonPath);

        jsonFile.Directory?.Create();

        await using var output = new FileStream(
            jsonFile.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await input.CopyToAsync(output, cancellationToken)
            .ConfigureAwait(false);

        return jsonFile;
    }

    private async Task<MinecraftEntry> ParseVersionAsync(
        MinecraftEntry seed,
        FileInfo versionJsonPath,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        UpdateStep(ParseVersionStep, 0, 1);

        await using var stream = new FileStream(
            versionJsonPath.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken)
            .ConfigureAwait(false);

        var entry = VersionJsonParser.MapEntry(document.RootElement, seed.Id);
        
        return entry with {
            InstancePath = seed.InstancePath,
            MinecraftVersion = seed.Id,
            Format = _layout.Format
        };
    }

    private async Task CompleteDependenciesAsync(
        MinecraftEntry entry,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        using var resourceDownloader = new ResourceDownloader(_source, _maxConcurrency, layout: _layout);
        resourceDownloader.ProgressChanged += OnResourceDownloadProgressChanged;

        try {
            var result = await resourceDownloader
                .DownloadAsync(entry, cancellationToken)
                .ConfigureAwait(false);

            if (result.FailCount > 0)
                throw new InvalidOperationException(
                    $"Some dependent files encountered errors during download. FailCount: {result.FailCount}");
        }
        finally {
            resourceDownloader.ProgressChanged -= OnResourceDownloadProgressChanged;
        }
    }

    private void OnResourceDownloadProgressChanged(object? sender, ResourceDownloadProgressChangedEventArgs args) => 
        UpdateStep(DownloadResourcesStep, args.CompletedCount, args.TotalCount);
}