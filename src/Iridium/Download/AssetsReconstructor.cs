using System.Text.Json;
using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;

namespace Iridium.Download;

/// <summary>
/// Reconstructs the un-hashed ("virtual") asset layout that legacy Minecraft versions
/// consume directly. Asset indexes marked <c>virtual</c> (1.6+) are laid out under
/// <c>assets/virtual/&lt;id&gt;</c>; indexes marked <c>map_to_resources</c> (pre-1.6)
/// are additionally copied into the game directory's <c>resources/</c> folder — the only
/// place those versions read sounds and textures from. Mirrors HMCL's reconstructAssets.
/// </summary>
public sealed class AssetsReconstructor {
    private readonly IMinecraftLayout _layout;

    public AssetsReconstructor(IMinecraftLayout? layout = null) {
        _layout = layout ?? new DefaultMinecraftLayoutFactory().Create(MinecraftFormat.Standard);
    }

    /// <summary>
    /// Resolves the assets directory that must be handed to the game via
    /// <c>${game_assets}</c>/<c>${assets_root}</c>: the virtual root for virtual indexes,
    /// otherwise the plain assets root.
    /// </summary>
    public string ResolveActualAssetsRoot(MinecraftEntry entry) {
        var assetsRoot = _layout.GetAssetsRoot(entry);
        var assetIndexId = GetAssetIndexId(entry);

        if (!TryGetIndexFlags(assetsRoot, assetIndexId, out var isVirtual, out _))
            return assetsRoot;

        if (!isVirtual)
            return assetsRoot;

        var virtualRoot = Path.Combine(assetsRoot, "virtual", assetIndexId);

        // Mirror HMCL: fall back to the hashed root when too few objects have been
        // materialised (e.g. incomplete download), so the game never sees a half-built dir.
        if (!HasEnoughObjects(assetsRoot, assetIndexId, virtualRoot))
            return assetsRoot;

        return virtualRoot;
    }

    /// <summary>
    /// Materialises the virtual layout (and, for map_to_resources indexes, the game
    /// directory's <c>resources/</c> folder) from the downloaded hashed asset objects.
    /// Returns the number of objects deployed. Idempotent: existing files are skipped.
    /// </summary>
    public Task<int> ReconstructAsync(MinecraftEntry entry, string gameDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => Reconstruct(entry, gameDirectory), cancellationToken);

    private int Reconstruct(MinecraftEntry entry, string gameDirectory) {
        var assetsRoot = _layout.GetAssetsRoot(entry);
        var assetIndexId = GetAssetIndexId(entry);

        if (!TryGetIndexFlags(assetsRoot, assetIndexId, out var isVirtual, out var mapToResources))
            return 0;

        if (!isVirtual)
            return 0;

        var objectsRoot = Path.Combine(assetsRoot, "objects");
        var virtualRoot = Path.Combine(assetsRoot, "virtual", assetIndexId);
        var resourcesRoot = mapToResources
            ? Path.Combine(gameDirectory, "resources")
            : null;

        var indexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");
        if (!File.Exists(indexPath))
            return 0;

        using var stream = new FileStream(
            indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("objects", out var objects))
            return 0;

        var deployed = 0;

        foreach (var asset in objects.EnumerateObject()) {
            if (!asset.Value.TryGetProperty("hash", out var hashElement) ||
                hashElement.GetString() is not { Length: > 0 } hash)
                continue;

            var source = Path.Combine(objectsRoot, hash[..2], hash);
            if (!File.Exists(source))
                continue;

            Deploy(source, Path.Combine(virtualRoot, asset.Name));

            if (resourcesRoot is not null)
                Deploy(source, Path.Combine(resourcesRoot, asset.Name));

            deployed++;
        }

        return deployed;
    }

    private static void Deploy(string source, string target) {
        if (File.Exists(target))
            return;

        var directory = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(directory))
            return;

        Directory.CreateDirectory(directory);
        File.Copy(source, target, overwrite: false);
    }

    private static bool HasEnoughObjects(string assetsRoot, string assetIndexId, string virtualRoot) {
        var indexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");
        if (!File.Exists(indexPath))
            return false;

        using var stream = new FileStream(
            indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);

        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("objects", out var objects))
            return false;

        var total = 0;
        var present = 0;

        foreach (var asset in objects.EnumerateObject()) {
            total++;
            if (File.Exists(Path.Combine(virtualRoot, asset.Name)))
                present++;
        }

        // HMCL treats a materialised share below 10% as "old format still in use".
        return total == 0 || present * 10 >= total;
    }

    private static bool TryGetIndexFlags(
        string assetsRoot,
        string assetIndexId,
        out bool isVirtual,
        out bool mapToResources) {
        isVirtual = false;
        mapToResources = false;

        var indexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");
        if (!File.Exists(indexPath))
            return false;

        using var stream = new FileStream(
            indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);

        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        if (!root.TryGetProperty("objects", out _))
            return false;

        mapToResources = root.TryGetProperty("map_to_resources", out var mapped)
            && mapped.ValueKind == JsonValueKind.True;

        isVirtual = mapToResources
            || (root.TryGetProperty("virtual", out var virtualFlag) && virtualFlag.ValueKind == JsonValueKind.True);

        return true;
    }

    private static string GetAssetIndexId(MinecraftEntry entry) => entry.AssetIndex?.Id ?? entry.Id;
}
