using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Minecraft;

namespace Iridium.Providers.Minecraft;

internal sealed class PrismMinecraftProvider : IMinecraftProvider {
    private readonly DirectoryInfo _root;

    public PrismMinecraftProvider(DirectoryInfo root) {
        _root = root;
    }

    public async Task<IReadOnlyList<MinecraftEntry>> GetMinecraftsAsync(CancellationToken cancellationToken = default) {
        var instancesDir = new DirectoryInfo(Path.Combine(_root.FullName, "instances"));
        if (!instancesDir.Exists)
            return [];

        var entries = new List<MinecraftEntry>();
        foreach (var dir in instancesDir.EnumerateDirectories()) {
            var entry = await ParseAsync(dir, cancellationToken);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }

    public async Task<MinecraftEntry?> GetMinecraftAsync(string id, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var dir = new DirectoryInfo(Path.Combine(_root.FullName, "instances", id));
        if (!dir.Exists)
            return null;

        return await ParseAsync(dir, cancellationToken);
    }

    private async Task<MinecraftEntry?> ParseAsync(DirectoryInfo dir, CancellationToken cancellationToken) {
        var packPath = Path.Combine(dir.FullName, "mmc-pack.json");
        if (!File.Exists(packPath))
            return null;

        var json = await File.ReadAllTextAsync(packPath, cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("components", out var components))
            return null;

        string? minecraftVersion = null;
        var loaders = new List<MinecraftLoader>();
        var componentList = new List<(string Uid, string Version)>();

        foreach (var component in components.EnumerateArray()) {
            var uid = component.TryGetProperty("uid", out var uidElement)
                ? uidElement.GetString()
                : null;
            if (uid is null)
                continue;

            var version = component.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;
            if (version is { Length: > 0 })
                componentList.Add((uid, version));

            if (uid == "net.minecraft") {
                minecraftVersion = version;
            }
            else if (ModLoaderDetector.TryMapComponentUid(uid, out var type) && !string.IsNullOrWhiteSpace(version)) {
                loaders.Add(new MinecraftLoader { Type = type, Version = version });
            }
        }

        if (string.IsNullOrWhiteSpace(minecraftVersion))
            return null;

        var entry = new MinecraftEntry {
            Id = dir.Name,
            Name = GetInstanceName(dir),
            MinecraftVersion = minecraftVersion,
            Loaders = loaders,
            InstancePath = dir.FullName,
            Format = MinecraftFormat.Prism
        };

        return await MergeComponentsAsync(entry, componentList, dir, cancellationToken);
    }

    private async Task<MinecraftEntry> MergeComponentsAsync(
        MinecraftEntry entry,
        IReadOnlyList<(string Uid, string Version)> components,
        DirectoryInfo dir,
        CancellationToken cancellationToken)
    {
        var docs = new List<(JsonDocument Document, string Uid)>();
        try {
            foreach (var (uid, version) in components) {
                var metaPath = Path.Combine(_root.FullName, "meta", uid, $"{version}.json");
                if (!File.Exists(metaPath))
                    continue;

                docs.Add((JsonDocument.Parse(await File.ReadAllTextAsync(metaPath, cancellationToken)), uid));
            }

            if (docs.Count == 0)
                return entry;

            string? mainClass = null;
            string? minecraftArguments = null;
            var libraries = new List<MinecraftLibrary>();
            var loaderLibraries = new List<MinecraftLibrary>();
            var mavenFiles = new List<MinecraftLibrary>();
            var seenMavenFiles = new HashSet<string>(StringComparer.Ordinal);
            JsonElement? minecraftRoot = null;

            foreach (var (document, uid) in docs.OrderBy(d => GetOrder(d.Document.RootElement))) {
                var root = document.RootElement;
                if (uid == "net.minecraft")
                    minecraftRoot = root;

                if (root.TryGetProperty("mainClass", out var mainClassElement) &&
                    mainClassElement.GetString() is { Length: > 0 } value)
                {
                    mainClass = value;
                }

                // Loader metas carry the full argument set (vanilla prefix included),
                // so the highest-order component that defines it wins.
                if (root.TryGetProperty("minecraftArguments", out var minecraftArgumentsElement) &&
                    minecraftArgumentsElement.GetString() is { Length: > 0 } arguments)
                {
                    minecraftArguments = arguments;
                }

                if (root.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array) {
                    var isLoader = ModLoaderDetector.TryMapComponentUid(uid, out _);
                    foreach (var library in VersionJsonParser.MapLibraries(librariesElement)) {
                        // Prism keeps a single version per artifact (the highest one), so
                        // e.g. guava 15.0 from vanilla and guava 17.0 from Forge don't both
                        // end up on the classpath in the wrong order.
                        AddLibrary(libraries, library);
                        if (isLoader)
                            AddLibrary(loaderLibraries, library);
                    }
                }

                // Maven files are downloaded into the shared libraries directory but are not
                // part of the classpath (Forge installer jar, modlauncher runtime files, ...).
                if (root.TryGetProperty("mavenFiles", out var mavenFilesElement) && mavenFilesElement.ValueKind == JsonValueKind.Array) {
                    foreach (var mavenFile in VersionJsonParser.MapLibraries(mavenFilesElement)) {
                        if (seenMavenFiles.Add(mavenFile.Name))
                            mavenFiles.Add(mavenFile);
                    }
                }
            }

            var merged = entry with {
                MainClass = mainClass,
                MinecraftArguments = minecraftArguments,
                Libraries = libraries,
                MavenFiles = mavenFiles,
                Tweakers = GetInstanceTweakers(dir)
            };

            // Legacy (launchwrapper) loaders declare their tweak class in the jar manifest
            // as TweakClass; launchwrapper 1.12 only reads --tweakClass args, so inject it.
            if (mainClass == "net.minecraft.launchwrapper.Launch") {
                var detectedTweakers = ReadTweakClasses(loaderLibraries, Path.Combine(_root.FullName, "libraries"));
                if (detectedTweakers.Count > 0)
                    merged = merged with { Tweakers = [.. detectedTweakers, .. merged.Tweakers] };
            }

            if (minecraftRoot is { } minecraftRootElement) {
                var assetIndexUrl = minecraftRootElement.TryGetProperty("assetIndex", out var assetIndex)
                    && assetIndex.TryGetProperty("url", out var assetIndexUrlElement)
                        ? assetIndexUrlElement.GetString()
                        : null;

                merged = merged with {
                    Arguments = VersionJsonParser.MapArguments(minecraftRootElement),
                    AssetIndex = assetIndex.TryGetProperty("id", out var assetId)
                        && assetId.GetString() is { Length: > 0 } assetIndexId
                        ? new AssetIndex(assetIndexId)
                        : merged.AssetIndex,
                    AssetIndexUrl = assetIndexUrl,
                    ClientDownload = MapMainJarDownload(minecraftRootElement),
                    Jar = minecraftRootElement.TryGetProperty("mainJar", out var mainJar)
                        && mainJar.TryGetProperty("name", out var mainJarName)
                        ? mainJarName.GetString()
                        : merged.Jar,
                    Type = VersionJsonParser.MapType(minecraftRootElement),
                    ReleaseTime = VersionJsonParser.MapReleaseTime(minecraftRootElement)
                };
            }

            return merged;
        } finally {
            foreach (var (document, _) in docs)
                document.Dispose();
        }
    }

    private static List<string> ReadTweakClasses(IReadOnlyList<MinecraftLibrary> libraries, string librariesRoot) {
        var result = new List<string>();
        foreach (var library in libraries) {
            var path = MavenPathParser.Resolve(librariesRoot, library.Name);
            if (path is null || !File.Exists(path))
                continue;

            try {
                using var archive = ZipFile.OpenRead(path);
                if (archive.GetEntry("META-INF/MANIFEST.MF") is not { } manifestEntry)
                    continue;

                using var reader = new StreamReader(manifestEntry.Open());
                if (ReadManifestAttribute(reader, "TweakClass") is { Length: > 0 } tweakClass)
                    result.Add(tweakClass);
            } catch {
                // Non-zip or corrupt file; skip.
            }
        }

        return result;
    }

    private static string? ReadManifestAttribute(TextReader reader, string targetKey) {
        var currentValue = new StringBuilder();
        string? result = null;

        while (reader.ReadLine() is { } line) {
            if (line.Length == 0) {
                currentValue.Clear();
                continue;
            }

            if (line[0] == ' ') {
                currentValue.Append(line.AsSpan(1));
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;

            var currentKey = line[..colon].Trim();
            currentValue.Clear();
            currentValue.Append(line.AsSpan(colon + 1).Trim());
            if (currentKey == targetKey)
                result = currentValue.ToString();
        }

        return result;
    }

    private static int GetOrder(JsonElement root) {
        if (root.TryGetProperty("order", out var order) && order.TryGetInt32(out var value))
            return value;

        return 0;
    }

    private static MinecraftFileDownload? MapMainJarDownload(JsonElement root) {
        if (!root.TryGetProperty("mainJar", out var mainJar) ||
            mainJar.ValueKind != JsonValueKind.Object ||
            !mainJar.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty("artifact", out var artifact) ||
            artifact.ValueKind != JsonValueKind.Object)
            return null;

        var url = artifact.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrEmpty(url))
            return null;

        return new MinecraftFileDownload {
            Url = url,
            Size = artifact.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0L,
            Sha1 = artifact.TryGetProperty("sha1", out var sha1Element) ? sha1Element.GetString() : null
        };
    }

    private static string GetInstanceName(DirectoryInfo dir) {
        var cfgPath = Path.Combine(dir.FullName, "instance.cfg");
        if (!File.Exists(cfgPath))
            return dir.Name;

        foreach (var line in File.ReadLines(cfgPath)) {
            if (!line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = line[5..].Trim();
            if (name.Length > 0)
                return name;
        }

        return dir.Name;
    }

    private static string[] GetInstanceTweakers(DirectoryInfo dir) {
        var cfgPath = Path.Combine(dir.FullName, "instance.cfg");
        if (!File.Exists(cfgPath))
            return [];

        foreach (var line in File.ReadLines(cfgPath)) {
            if (!line.StartsWith("tweakers=", StringComparison.OrdinalIgnoreCase))
                continue;

            return line["tweakers=".Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }

    /// <summary>
    /// Adds a library keeping a single version per group:artifact:classifier, replacing an
    /// existing entry only when the incoming version is higher (mirrors Prism's applyLibrary).
    /// </summary>
    private static void AddLibrary(List<MinecraftLibrary> libraries, MinecraftLibrary library) {
        var key = GetArtifactKey(library.Name);
        for (var i = 0; i < libraries.Count; i++) {
            if (!string.Equals(GetArtifactKey(libraries[i].Name), key, StringComparison.Ordinal))
                continue;

            if (CompareVersions(GetLibraryVersion(library.Name), GetLibraryVersion(libraries[i].Name)) > 0)
                libraries[i] = library;

            return;
        }

        libraries.Add(library);
    }

    private static string GetArtifactKey(string name) {
        var parts = name.Split(':');
        if (parts.Length < 2)
            return name;

        // group:artifact[:classifier] -- natives like lwjgl-...:3.3.1:natives-linux must not
        // be collapsed into the plain artifact.
        var key = $"{parts[0]}:{parts[1]}";
        if (parts.Length >= 4)
            key += $":{parts[3]}";

        return key;
    }

    private static string GetLibraryVersion(string name) {
        var parts = name.Split(':');
        return parts.Length >= 3 ? parts[2] : string.Empty;
    }

    private static int CompareVersions(string a, string b) {
        var aParts = a.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var bParts = b.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Max(aParts.Length, bParts.Length);

        for (var i = 0; i < count; i++) {
            var x = i < aParts.Length ? aParts[i] : string.Empty;
            var y = i < bParts.Length ? bParts[i] : string.Empty;
            if (x == y)
                continue;

            if (int.TryParse(x, out var xi) && int.TryParse(y, out var yi))
            {
                var numeric = xi.CompareTo(yi);
                if (numeric != 0)
                    return numeric;
            }
            else
            {
                var ordinal = string.CompareOrdinal(x, y);
                if (ordinal != 0)
                    return ordinal;
            }
        }

        return 0;
    }
}