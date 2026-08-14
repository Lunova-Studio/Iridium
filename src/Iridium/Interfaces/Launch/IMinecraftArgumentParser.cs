using Iridium.Interfaces.Minecraft;
using Iridium.Models.Launch;
using Iridium.Models.Minecraft;

namespace Iridium.Interfaces.Launch;

public interface IMinecraftArgumentParser {
    LaunchArguments Build(MinecraftEntry entry, LaunchConfig config);
}
