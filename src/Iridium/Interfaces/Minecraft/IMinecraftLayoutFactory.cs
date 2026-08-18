using Iridium.Enums;

namespace Iridium.Interfaces.Minecraft;

public interface IMinecraftLayoutFactory {
    IMinecraftLayout Create(MinecraftFormat format);
}
