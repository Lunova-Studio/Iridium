using System.Text.Json;
using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;

namespace Iridium.Installation;

/// <summary>
/// Reconstructs the un-hashed ("virtual") asset layout that legacy Minecraft versions
/// consume directly. Asset indexes marked <c>virtual</c> (1.6+) are laid out under
/// <c>assets/virtual/&lt;id&gt;</c>; indexes marked <c>map_to_resources</c> (pre-1.6)
/// are additionally copied into the game directory's <c>resources/</c> folder — the only
/// place those versions read sounds and textures from. Mirrors HMCL reconstructAssets.
/// </summary>
internal sealed class AssetsReconstructor {
    private const int BufferSize = 64 * 1024;

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

        if (!TryGetIndexFlags(assetsRoot, assetIndexId, out var isVirtual) || !isVirtual)
            return assetsRoot;

        var virtualRoot = Path.Combine(assetsRoot, "virtual", assetIndexId);

        // Fall back to the hashed object root when the virtual layout has not
        // been materialized sufficiently. This prevents the game from seeing
        // an incomplete virtual asset tree.
        return HasEnoughObjects(assetsRoot, assetIndexId, virtualRoot)
            ? virtualRoot
            : assetsRoot;
    }

    /// <summary>
    /// Materializes the virtual layout and, for map_to_resources indexes,
    /// the game's <c>resources/</c> directory from the downloaded hashed
    /// asset objects.
    /// </summary>
    /// <returns>
    /// The number of asset objects for which at least one target was created.
    /// </returns>
    public async Task ReconstructAsync(
        MinecraftEntry entry,
        string gameDirectory,
        CancellationToken cancellationToken = default) {
        var assetsRoot = _layout.GetAssetsRoot(entry);
        var assetIndexId = GetAssetIndexId(entry);

        var indexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (!File.Exists(indexPath)) 
            return;

        using var document = await ReadIndexAsync(indexPath, cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;

        if (!root.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Object)
            return;

        var isVirtual = 
            root.TryGetProperty("virtual", out var virtualFlag) &&
            virtualFlag.ValueKind == JsonValueKind.True;

        var mapToResources =
            root.TryGetProperty("map_to_resources", out var mappedFlag) &&
            mappedFlag.ValueKind == JsonValueKind.True;

        if (!isVirtual && !mapToResources)
            return;

        var objectsRoot = Path.Combine(assetsRoot, "objects");
        var virtualRoot = Path.Combine(assetsRoot, "virtual", assetIndexId);

        var resourcesRoot = mapToResources
            ? Path.Combine(gameDirectory, "resources")
            : null;

        foreach (var asset in objects.EnumerateObject()) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!asset.Value.TryGetProperty("hash", out var hashElement) ||
                hashElement.ValueKind != JsonValueKind.String) {
                continue;
            }

            var hash = hashElement.GetString();

            if (string.IsNullOrEmpty(hash) || hash.Length < 2)
                continue;

            var source = Path.Combine(
                objectsRoot,
                hash[..2],
                hash);

            if (!File.Exists(source))
                continue;

            var objectDeployed = false;

            var virtualTarget = Path.Combine(
                virtualRoot,
                asset.Name);

            if (await DeployAsync(
                    source,
                    virtualTarget,
                    cancellationToken).ConfigureAwait(false)) {
                objectDeployed = true;
            }

            if (resourcesRoot is not null) {
                var resourcesTarget = Path.Combine(
                    resourcesRoot,
                    asset.Name);

                if (await DeployAsync(
                        source,
                        resourcesTarget,
                        cancellationToken).ConfigureAwait(false)) {
                    objectDeployed = true;
                }
            }

            if (objectDeployed) { }
        }
    }

    private static async Task<JsonDocument> ReadIndexAsync(
        string indexPath,
        CancellationToken cancellationToken) {
        await using var stream = new FileStream(
            indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<bool> DeployAsync(
        string source,
        string target,
        CancellationToken cancellationToken) {
        if (File.Exists(target))
            return false;

        var directory = Path.GetDirectoryName(target);

        if (string.IsNullOrEmpty(directory))
            return false;

        Directory.CreateDirectory(directory);

        // Copy to a temporary file first so cancellation or an I/O failure
        // cannot leave a partially-written target that would be mistaken
        // for a completed asset on the next reconstruction.
        var temporaryTarget = $"{target}.{Guid.NewGuid():N}.tmp";

        try {
            await CopyFileAsync(source, temporaryTarget, cancellationToken)
                .ConfigureAwait(false);

            try {
                File.Move(temporaryTarget, target, false);
            }
            catch (IOException) when (File.Exists(target)) {
                // Another process created the target between our initial
                // existence check and the final move.
                return false;
            }

            return true;
        }
        finally {
            TryDeleteFile(temporaryTarget);
        }
    }

    private static async Task CopyFileAsync(
        string source,
        string target,
        CancellationToken cancellationToken) {
        await using var sourceStream = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var targetStream = new FileStream(
            target,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await sourceStream.CopyToAsync(
            targetStream,
            BufferSize,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool HasEnoughObjects(
        string assetsRoot,
        string assetIndexId,
        string virtualRoot) {
        var indexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (!File.Exists(indexPath))
            return false;

        using var stream = new FileStream(
            indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.SequentialScan);

        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Object)
            return false;

        var total = 0;
        var present = 0;

        foreach (var asset in objects.EnumerateObject()) {
            total++;

            if (File.Exists(Path.Combine(virtualRoot, asset.Name)))
                present++;
        }

        if (total == 0)
            return false;

        // Treat a materialized share of at least 10% as usable.
        return present * 10L >= total;
    }

    private static bool TryGetIndexFlags(
        string assetsRoot,
        string assetIndexId,
        out bool isVirtual) {
        isVirtual = false;

        var indexPath = Path.Combine(
            assetsRoot,
            "indexes",
            $"{assetIndexId}.json");

        if (!File.Exists(indexPath))
            return false;

        using var stream = new FileStream(
            indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.SequentialScan);

        using var document = JsonDocument.Parse(stream);

        var root = document.RootElement;

        if (!root.TryGetProperty("objects", out var objects) ||
            objects.ValueKind != JsonValueKind.Object) {
            return false;
        }

        var mapToResources = root.TryGetProperty("map_to_resources", out var mapped) && 
                             mapped.ValueKind == JsonValueKind.True; 
        
        isVirtual = mapToResources || (root.TryGetProperty("virtual", out var virtualFlag) && 
                                       virtualFlag.ValueKind == JsonValueKind.True);

        return true;
    }

    private static void TryDeleteFile(string path) {
        try {
            File.Delete(path);
        }
        catch {
            // Best-effort cleanup. The original exception, if any,
            // must not be hidden by temporary-file cleanup.
        }
    }

    private static string GetAssetIndexId(MinecraftEntry entry) =>
        entry.AssetIndex?.Id ?? entry.Id;
}