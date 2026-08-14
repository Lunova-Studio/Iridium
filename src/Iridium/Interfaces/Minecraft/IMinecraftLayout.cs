using Iridium.Models.Minecraft;

namespace Iridium.Interfaces.Minecraft;

public interface IMinecraftLayout {
    string GetInstanceRoot(MinecraftEntry entry);
    string GetGameDirectory(MinecraftEntry entry);
    string GetLibrariesRoot(MinecraftEntry entry);
    string GetAssetsRoot(MinecraftEntry entry);
    string GetNativesDirectory(MinecraftEntry entry);
    string GetVersionJarPath(MinecraftEntry entry);
}
