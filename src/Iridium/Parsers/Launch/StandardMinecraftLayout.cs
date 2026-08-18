using Iridium.Helpers;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;

namespace Iridium.Parsers.Launch;

public sealed class StandardMinecraftLayout : IMinecraftLayout {
    public string GetInstanceRoot(MinecraftEntry entry) => GetRoot(entry);

    public string GetGameDirectory(MinecraftEntry entry) {
        var versionDir = GetVersionDirectory(entry);

        // Version-isolated instances (HMCL etc.) keep game files inside the version directory.
        if (File.Exists(Path.Combine(versionDir, "options.txt"))
            || Directory.Exists(Path.Combine(versionDir, "mods"))
            || Directory.Exists(Path.Combine(versionDir, "saves")))
            return versionDir;

        return GetRoot(entry);
    }

    public string GetLibrariesRoot(MinecraftEntry entry) => Path.Combine(GetRoot(entry), "libraries");

    public string GetAssetsRoot(MinecraftEntry entry) => Path.Combine(GetRoot(entry), "assets");

    public string GetNativesDirectory(MinecraftEntry entry) => Path.Combine(GetVersionDirectory(entry), $"natives-{PlatformHelper.GetPlatformInfo()}");

    public string GetVersionJarPath(MinecraftEntry entry) {
        var jarName = string.IsNullOrEmpty(entry.Jar) ? entry.Id : entry.Jar;
        return Path.Combine(GetVersionDirectory(entry), $"{jarName}.jar");
    }

    public string GetGameDirectory(string id) => Path.Combine("versions", id);

    public string GetNativesDirectory(string id) => Path.Combine("versions", id, $"natives-{PlatformHelper.GetPlatformInfo()}");

    public string GetVersionJarPath(string id) => Path.Combine("versions", id, $"{id}.jar");

    public string GetVersionJsonPath(string id) => Path.Combine("versions", id, $"{id}.json");

    public string GetVersionJsonPath(MinecraftEntry entry) => Path.Combine(GetRoot(entry), "versions", entry.Id, $"{entry.Id}.json");

    private static string GetVersionDirectory(MinecraftEntry entry) {
        return string.IsNullOrEmpty(entry.InstancePath) 
            ? entry.InstancePath 
            : Path.GetFullPath(entry.InstancePath);
    }

    private static string GetRoot(MinecraftEntry entry) {
        var versionDir = GetVersionDirectory(entry);
        if (versionDir.Length == 0)
            return versionDir;

        return Path.GetDirectoryName(Path.GetDirectoryName(versionDir)) ?? versionDir;
    }
}
