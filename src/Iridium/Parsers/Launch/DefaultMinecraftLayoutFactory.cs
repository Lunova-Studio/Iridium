using Iridium.Enums;
using Iridium.Interfaces.Minecraft;

namespace Iridium.Parsers.Launch;

public sealed class DefaultMinecraftLayoutFactory : IMinecraftLayoutFactory {
    public IMinecraftLayout Create(MinecraftFormat format) =>
        format == MinecraftFormat.Prism ? new PrismMinecraftLayout() : new StandardMinecraftLayout();
}
