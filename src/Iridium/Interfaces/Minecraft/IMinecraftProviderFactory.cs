using Iridium.Providers;

namespace Iridium.Interfaces.Minecraft;

public interface IMinecraftProviderFactory {
    IMinecraftProvider Create(DirectoryInfo root);
}