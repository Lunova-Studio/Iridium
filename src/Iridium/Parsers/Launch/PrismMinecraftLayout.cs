using Iridium.Helpers;
using Iridium.Interfaces;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Minecraft;

namespace Iridium.Minecraft.Layout;

public sealed class PrismMinecraftLayout : IMinecraftLayout {
    public string GetInstanceRoot(MinecraftEntry entry) => entry.InstancePath;

    public string GetGameDirectory(MinecraftEntry entry) {
        var flat = Path.Combine(entry.InstancePath, "minecraft");
        if (Directory.Exists(flat))
            return flat;

        var full = Path.Combine(entry.InstancePath, ".minecraft");
        return Directory.Exists(full) ? full : entry.InstancePath;
    }

    public string GetLibrariesRoot(MinecraftEntry entry) => Path.Combine(GetPrismRoot(entry), "libraries");

    public string GetAssetsRoot(MinecraftEntry entry) => Path.Combine(GetPrismRoot(entry), "assets");

    public string GetNativesDirectory(MinecraftEntry entry) =>
        Path.Combine(entry.InstancePath, $"natives-{PlatformHelper.GetPlatformInfo()}");

    public string GetVersionJarPath(MinecraftEntry entry) {
        // The client jar of a prism instance is the mainJar maven artifact resolved
        // from the global library store.
        if (!string.IsNullOrEmpty(entry.Jar) && MavenPathParser.Resolve(GetLibrariesRoot(entry), entry.Jar) is { } jarPath)
            return jarPath;

        return Path.Combine(GetGameDirectory(entry), $"{entry.Id}.jar");
    }

    private static string GetPrismRoot(MinecraftEntry entry) {
        if (string.IsNullOrEmpty(entry.InstancePath))
            return entry.InstancePath;

        var instanceDir = Path.GetFullPath(entry.InstancePath);
        return Path.GetDirectoryName(Path.GetDirectoryName(instanceDir)) ?? instanceDir;
    }
}
