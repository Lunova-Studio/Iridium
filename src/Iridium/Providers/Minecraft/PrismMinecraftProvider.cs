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
            var seenLibraries = new HashSet<string>(StringComparer.Ordinal);
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
                        if (!seenLibraries.Add(library.Name))
                            continue;

                        libraries.Add(library);
                        if (isLoader)
                            loaderLibraries.Add(library);
                    }
                }
            }

            var merged = entry with {
                MainClass = mainClass,
                MinecraftArguments = minecraftArguments,
                Libraries = libraries,
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
                merged = merged with {
                    AssetIndex = minecraftRootElement.TryGetProperty("assetIndex", out var assetIndex)
                        && assetIndex.TryGetProperty("id", out var assetId)
                        && assetId.GetString() is { Length: > 0 } assetIndexId
                        ? new AssetIndex(assetIndexId)
                        : merged.AssetIndex,
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
}