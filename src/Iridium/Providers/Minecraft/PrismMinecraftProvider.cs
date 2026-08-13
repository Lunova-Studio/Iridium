using System.Text.Json;
using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;

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

    private static async Task<MinecraftEntry?> ParseAsync(DirectoryInfo dir, CancellationToken cancellationToken) {
        var packPath = Path.Combine(dir.FullName, "mmc-pack.json");
        if (!File.Exists(packPath))
            return null;

        var json = await File.ReadAllTextAsync(packPath, cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("components", out var components))
            return null;

        string? minecraftVersion = null;
        var loaders = new List<MinecraftLoader>();

        foreach (var component in components.EnumerateArray()) {
            var uid = component.TryGetProperty("uid", out var uidElement)
                ? uidElement.GetString()
                : null;
            if (uid is null)
                continue;

            var version = component.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;

            if (uid == "net.minecraft") {
                minecraftVersion = version;
            }
            else if (ModLoaderDetector.TryMapComponentUid(uid, out var type) && !string.IsNullOrWhiteSpace(version)) {
                loaders.Add(new MinecraftLoader { Type = type, Version = version });
            }
        }

        if (string.IsNullOrWhiteSpace(minecraftVersion))
            return null;

        return new MinecraftEntry {
            Id = dir.Name,
            Name = GetInstanceName(dir),
            MinecraftVersion = minecraftVersion,
            Loaders = loaders,
            InstancePath = dir.FullName,
            Format = MinecraftFormat.Prism
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
}