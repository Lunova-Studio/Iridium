using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Minecraft;

namespace Iridium.Parsers.Launch;

public sealed class PrismMinecraftLayout : IMinecraftLayout {
    public MinecraftFormat Format => MinecraftFormat.Prism;

    public string GetInstanceDirectory(string id) => Path.Combine("instances", id);

    public string GetInstanceRoot(MinecraftEntry entry) => entry.InstancePath;

    public string GetGameDirectory(MinecraftEntry entry) {
        var mcDir = Path.Combine(entry.InstancePath, "minecraft");
        var dotMcDir = Path.Combine(entry.InstancePath, ".minecraft");

        // Prism always reports <instance>/minecraft unless a legacy <instance>/.minecraft exists.
        if (Directory.Exists(dotMcDir) && !Directory.Exists(mcDir))
            return dotMcDir;

        return mcDir;
    }

    public string GetLibrariesRoot(MinecraftEntry entry) => Path.Combine(GetPrismRoot(entry), "libraries");

    public string GetAssetsRoot(MinecraftEntry entry) => Path.Combine(GetPrismRoot(entry), "assets");

    public string GetNativesDirectory(MinecraftEntry entry) => Path.Combine(entry.InstancePath, "natives");

    public string GetVersionJarPath(MinecraftEntry entry) {
        if (!string.IsNullOrEmpty(entry.Jar) && MavenPathParser.Resolve(GetLibrariesRoot(entry), entry.Jar) is { } jarPath)
            return jarPath;

        // Vanilla instances: Prism keeps the client jar as com.mojang:minecraft:<mc>:client in the shared libraries dir.
        if (!string.IsNullOrEmpty(entry.MinecraftVersion) &&
            MavenPathParser.Resolve(GetLibrariesRoot(entry), $"com.mojang:minecraft:{entry.MinecraftVersion}:client") is { } vanillaJarPath)
            return vanillaJarPath;

        return Path.Combine(GetGameDirectory(entry), $"{entry.Id}.jar");
    }

    public string GetVersionJsonPath(MinecraftEntry entry) {
        var patchPath = Path.Combine(entry.InstancePath, "patches", "net.minecraft.json");
        if (File.Exists(patchPath))
            return patchPath;

        // Instances created by Prism Launcher itself keep the net.minecraft component in
        // the shared meta cache instead of a local patch.
        if (!string.IsNullOrEmpty(entry.MinecraftVersion)) {
            var metaPath = Path.Combine(GetPrismRoot(entry), "meta", "net.minecraft", $"{entry.MinecraftVersion}.json");
            if (File.Exists(metaPath))
                return metaPath;
        }

        return patchPath;
    }

    private static string GetPrismRoot(MinecraftEntry entry) {
        if (string.IsNullOrEmpty(entry.InstancePath))
            return entry.InstancePath;

        var instanceDir = Path.GetFullPath(entry.InstancePath);
        return Path.GetDirectoryName(Path.GetDirectoryName(instanceDir)) ?? instanceDir;
    }
}
