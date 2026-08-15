using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;

namespace Iridium.Parsers.Launch;

/// <summary>
/// Builds launch arguments for Prism Launcher instances. The entry manifest is fully
/// resolved by the Prism provider (components merged from prism's metadata store); the
/// manifest-driven argument assembly is shared with the standard resolver.
/// </summary>
public sealed class PrismMinecraftArgumentParser : StandardMinecraftArgumentParser {
    public override IMinecraftLayout CreateLayout(MinecraftEntry entry) => 
        new PrismMinecraftLayout();
}