using Iridium.Enums;
using Iridium.Models.Minecraft;

namespace Iridium.Interfaces.Minecraft;

public interface IMinecraftLayout {
    MinecraftFormat Format { get; }

    /// <summary>
    /// Root-relative path of an instance directory, e.g. "versions/1.21".
    /// </summary>
    string GetInstanceDirectory(string id);

    string GetInstanceRoot(MinecraftEntry entry);
    string GetGameDirectory(MinecraftEntry entry);
    string GetLibrariesRoot(MinecraftEntry entry);
    string GetAssetsRoot(MinecraftEntry entry);
    string GetNativesDirectory(MinecraftEntry entry);
    string GetVersionJarPath(MinecraftEntry entry);
    string GetVersionJsonPath(MinecraftEntry entry);
}
