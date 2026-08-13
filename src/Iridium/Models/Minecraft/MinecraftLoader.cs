using Iridium.Enums;

namespace Iridium.Models.Minecraft;

public sealed record MinecraftLoader {
    public LoaderType Type { get; init; }
    public string Version { get; init; } = string.Empty;
}