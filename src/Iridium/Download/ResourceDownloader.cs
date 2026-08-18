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
    private readonly IMinecraftLayoutFactory _factory;
    private readonly IMinecraftLayout? _layout;
    private readonly DefaultDownloader _downloader;
    private readonly Action<ResourceDownloadProgressChangedEventArgs> _forwardProgress;

    private int _disposed;
    
    public event EventHandler<ResourceDownloadProgressChangedEventArgs>? ProgressChanged;
    
    public ResourceDownloader(DownloadSource source, int maxConcurrency = 4, IMinecraftLayoutFactory? factory = null, IMinecraftLayout? layout = null) {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _factory = factory ?? new DefaultMinecraftLayoutFactory();
        _layout = layout;
        _forwardProgress = ForwardProgress;
        _downloader = new DefaultDownloader(maxConcurrency);
    }

    public async Task<DownloadResponse> DownloadAsync(MinecraftEntry entry, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(entry);

        var layout = _layout ?? _factory.Create(entry.Format);
        var files = ResolveFiles(entry, layout);

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

    private List<DownloadFileEntry> ResolveFiles(
        MinecraftEntry entry,
        IMinecraftLayout layout) {
        var files = new List<DownloadFileEntry>(entry.Libraries.Count + 64);
        var librariesRoot = layout.GetLibrariesRoot(entry);
        var assetsRoot = layout.GetAssetsRoot(entry);

        var versionJarPath = layout.GetVersionJarPath(entry);

        if (!File.Exists(versionJarPath) && entry.ClientDownload is { Url.Length: > 0 } client) {
            files.Add(new DownloadFileEntry {
                Type = DownloadFileType.ClientJar,
                LocalPath = versionJarPath,
                Url = client.Url,
                Size = client.Size,
                Sha1 = client.Sha1,
                VersionId = entry.Id
            });
        }

        foreach (var library in EnumerateLibraries(entry)) {
            if (library.Natives is { Count: > 0 } natives) {
                AddNativeLibraryDownload(files, librariesRoot, library, natives);
                continue;
            }

            var mavenPath = ResolveLibraryPath(librariesRoot, library);

            if (mavenPath is null || File.Exists(mavenPath))
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
                Url = ResolveLibraryUrl(library.Url, libEntry)
            });
        }

        var assetIndexId = entry.AssetIndex?.Id ?? entry.Id;
        var assetIndexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (!File.Exists(assetIndexPath) && entry.AssetIndexUrl is { Length: > 0 } url) {
            files.Add(new DownloadFileEntry {
                Type = DownloadFileType.AssetIndex,
                LocalPath = assetIndexPath,
                Url = url,
                VersionId = assetIndexId
            });
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

    private static IEnumerable<MinecraftLibrary> EnumerateLibraries(MinecraftEntry entry) {
        foreach (var library in entry.Libraries)
            yield return library;

        foreach (var mavenFile in entry.MavenFiles)
            yield return mavenFile;
    }

    private void AddNativeLibraryDownload(
        List<DownloadFileEntry> files,
        string librariesRoot,
        MinecraftLibrary library,
        IReadOnlyDictionary<string, string> natives) {
        if (!VersionArgumentRuleParser.IsActive(library.Rules, []))
            return;

        if (VersionArgumentRuleParser.GetNativeClassifier(natives) is not { } classifier)
            return;

        // The metadata only ships natives for the platforms it declares (e.g. twitch is
        // Windows/macOS only), so don't invent a download for an undeclared classifier.
        if (library.ClassifierUrls is not null && !library.ClassifierUrls.ContainsKey(classifier))
            return;

        var nativeName = $"{library.Name}:{classifier}";
        var nativePath = MavenPathParser.Resolve(librariesRoot, nativeName);
        if (nativePath is null || File.Exists(nativePath))
            return;

        var relativePath = Path
            .GetRelativePath(librariesRoot, nativePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var libEntry = new DownloadFileEntry {
            Type = DownloadFileType.Library,
            LibraryPath = relativePath
        };

        var classifierUrl = library.ClassifierUrls is not null &&
            library.ClassifierUrls.TryGetValue(classifier, out var url)
                ? url
                : null;

        files.Add(new DownloadFileEntry {
            Type = DownloadFileType.Library,
            LocalPath = nativePath,
            Url = ResolveLibraryUrl(classifierUrl, libEntry)
        });
    }

    private static string? ResolveLibraryPath(string librariesRoot, MinecraftLibrary library) {
        if (library.Path is { Length: > 0 } relative)
            return Path.Combine(librariesRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        return MavenPathParser.Resolve(librariesRoot, library.Name);
    }

    private string ResolveLibraryUrl(string? metadataUrl, DownloadFileEntry file) {
        // Third-party metadata (Forge etc.) pins its own download host; use it verbatim.
        // Mojang-hosted artifacts keep flowing through the DownloadSource so mirrors
        // (BmclApi) can rewrite them.
        if (metadataUrl is { Length: > 0 } && !IsMojangHosted(metadataUrl))
            return metadataUrl;

        return _source.GetUrl(file);
    }

    private static bool IsMojangHosted(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host is "libraries.minecraft.net" or "resources.download.minecraft.net"
            || uri.Host.EndsWith(".mojang.com", StringComparison.Ordinal);
    }
}