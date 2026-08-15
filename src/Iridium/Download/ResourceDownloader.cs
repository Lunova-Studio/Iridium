using System.Text.Json;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Download;
using Iridium.Models.Minecraft;
using Iridium.Launch;
using Iridium.Parsers.Launch;
using Iridium.Parsers.Minecraft;

namespace Iridium.Download;

public sealed class ResourceDownloader : IDisposable {
    private readonly DownloadSource _source;
    private readonly IMinecraftLayout? _layout;
    private readonly DefaultDownloader _downloader;
    private readonly Action<ResourceDownloadProgressChangedEventArgs> _forwardProgress;

    private int _disposed;
    
    public event EventHandler<ResourceDownloadProgressChangedEventArgs>? ProgressChanged;
    
    public ResourceDownloader(DownloadSource source, int maxConcurrency = 4, IMinecraftLayout? layout = null) {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _layout = layout;
        _forwardProgress = ForwardProgress;
        _downloader = new DefaultDownloader(maxConcurrency);
    }

    public async Task<DownloadResponse> DownloadAsync(MinecraftEntry entry, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(entry);

        var layout = _layout ?? MinecraftLayoutFactory.Create(entry);
        var files = await ResolveFilesAsync(entry, layout, cancellationToken)
            .ConfigureAwait(false);

        DownloadFileEntry? assetIndex = null;
        var assetIndexPos = -1;

        for (var i = 0; i < files.Count; i++) {
            if (files[i].Type != DownloadFileType.AssetIndex)
                continue;

            assetIndex = files[i];
            assetIndexPos = i;
            break;
        }

        if (assetIndex is not null) {
            var indexResult = await _downloader.DownloadManyAsync([
                    new DownloadRequest {
                        Url = assetIndex.Url,
                        LocalPath = assetIndex.LocalPath,
                        Size = assetIndex.Size
                    }
                ], _forwardProgress, cancellationToken)
                .ConfigureAwait(false);

            if (indexResult.FailCount > 0)
                return indexResult;

            files.RemoveAt(assetIndexPos);
        }

        var assetsRoot = layout.GetAssetsRoot(entry);
        var assetIndexId = entry.AssetIndex?.Id ?? entry.Id;
        var assetIndexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (File.Exists(assetIndexPath)) {
            await using var assetStream = new FileStream(
                assetIndexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var assetDoc = await JsonDocument.ParseAsync(assetStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (assetDoc.RootElement.TryGetProperty("objects", out var objects)) {
                foreach (var asset in objects.EnumerateObject()) {
                    var hash = asset.Value.GetProperty("hash")
                        .GetString()!;

                    var size = asset.Value.TryGetProperty("size", out var sizeElement) 
                        ? sizeElement.GetInt64()
                        : 0L;

                    var assetPath = Path.Combine(assetsRoot, "objects", hash[..2], hash);

                    if (File.Exists(assetPath))
                        continue;

                    var assetEntry = new DownloadFileEntry {
                        Type = DownloadFileType.Asset,
                        Hash = hash
                    };

                    files.Add(new DownloadFileEntry {
                        Type = DownloadFileType.Asset,
                        LocalPath = assetPath,
                        Hash = hash,
                        Size = size,
                        Url = _source.GetUrl(assetEntry)
                    });
                }
            }
        }

        if (files.Count == 0)
            return new DownloadResponse {
                SuccessCount = 0
            };

        var downloadRequests = new List<DownloadRequest>(files.Count);

        foreach (var file in files)
            downloadRequests.Add(new DownloadRequest {
                Url = file.Url,
                LocalPath = file.LocalPath,
                Size = file.Size
            });

        return await _downloader.DownloadManyAsync(downloadRequests, _forwardProgress, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<DownloadFileEntry>> ResolveFilesAsync(
        MinecraftEntry entry,
        IMinecraftLayout layout,
        CancellationToken cancellationToken) {
        var files = new List<DownloadFileEntry>(entry.Libraries.Count + 64);
        var root = layout.GetInstanceRoot(entry);
        var librariesRoot = layout.GetLibrariesRoot(entry);
        var assetsRoot = layout.GetAssetsRoot(entry);

        var versionJsonPath = Path.Combine(root, "versions", entry.Id, $"{entry.Id}.json");

        if (!File.Exists(versionJsonPath))
            throw new InvalidOperationException(
                $"Version JSON not found: {versionJsonPath}");

        await using var versionStream = new FileStream(
            versionJsonPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var versionDoc = await JsonDocument.ParseAsync(versionStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var versionRoot = versionDoc.RootElement;
        var versionJarPath = layout.GetVersionJarPath(entry);

        if (!File.Exists(versionJarPath)) {
            var fileEntry = new DownloadFileEntry {
                Type = DownloadFileType.ClientJar,
                LocalPath = versionJarPath,
                VersionId = entry.Id
            };

            if (versionRoot.TryGetProperty("downloads", out var downloads) &&
                downloads.TryGetProperty("client", out var client) &&
                client.TryGetProperty("url", out var urlElement)) {
                fileEntry.Url = urlElement.GetString()!;

                fileEntry.Size = client.TryGetProperty("size", out var sizeElement) 
                    ? sizeElement.GetInt64() 
                    : 0L;
            }

            files.Add(fileEntry);
        }

        foreach (var library in entry.Libraries) {
            var mavenPath = MavenPathParser.Resolve(librariesRoot, library.Name);

            if (mavenPath is null || File.Exists(mavenPath))
                continue;

            if (library.Natives is { Count: > 0 })
                continue;

            if (!VersionArgumentRuleParser.IsActive(library.Rules, []))
                continue;

            var relativePath = Path
                .GetRelativePath(librariesRoot, mavenPath)
                .Replace(Path.DirectorySeparatorChar, '/');

            var libEntry = new DownloadFileEntry {
                Type = DownloadFileType.Library,
                LibraryPath = relativePath
            };

            files.Add(new DownloadFileEntry {
                Type = DownloadFileType.Library,
                LocalPath = mavenPath,
                Url = _source.GetUrl(libEntry)
            });
        }

        var assetIndexId = entry.AssetIndex?.Id ?? entry.Id;
        var assetIndexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (!File.Exists(assetIndexPath)) {
            var fileEntry = new DownloadFileEntry {
                Type = DownloadFileType.AssetIndex,
                LocalPath = assetIndexPath,
                VersionId = assetIndexId
            };

            if (versionRoot.TryGetProperty("assetIndex", out var assetIndex) &&
                assetIndex.TryGetProperty("url", out var urlElement)) 
                fileEntry.Url = urlElement.GetString()!;

            files.Add(fileEntry);
        }

        return files;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _downloader.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    
    private void ForwardProgress(ResourceDownloadProgressChangedEventArgs args) => ProgressChanged?.Invoke(this, args);
}