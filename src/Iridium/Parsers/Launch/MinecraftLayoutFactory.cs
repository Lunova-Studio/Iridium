using Iridium.Enums;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;

namespace Iridium.Parsers.Launch;

internal static class MinecraftLayoutFactory {
    public static IMinecraftLayout Create(MinecraftEntry entry)
        => entry.Format == MinecraftFormat.Prism ? new PrismMinecraftLayout() : new StandardMinecraftLayout();
}
