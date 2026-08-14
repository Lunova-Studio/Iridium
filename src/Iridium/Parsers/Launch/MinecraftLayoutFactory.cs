using Iridium.Enums;
using Iridium.Interfaces;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;

namespace Iridium.Minecraft.Layout;

internal static class MinecraftLayoutFactory {
    public static IMinecraftLayout Create(MinecraftEntry entry)
        => entry.Format == MinecraftFormat.Prism ? new PrismMinecraftLayout() : new StandardMinecraftLayout();
}
