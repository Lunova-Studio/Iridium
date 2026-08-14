using Iridium.Enums;
using Iridium.Interfaces.Launch;
using Iridium.Models.Minecraft;

namespace Iridium.Parsers.Launch;

public static class MinecraftArgumentParserFactory {
    public static IMinecraftArgumentParser Create(MinecraftEntry entry)
        => entry.Format == MinecraftFormat.Prism
            ? new PrismMinecraftArgumentParser()
            : new StandardMinecraftArgumentParser();
}
