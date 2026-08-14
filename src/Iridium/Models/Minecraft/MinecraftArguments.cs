namespace Iridium.Models.Minecraft;

public sealed record MinecraftArguments {
    public IReadOnlyList<MinecraftArgument> Game { get; init; } = [];
    public IReadOnlyList<MinecraftArgument> Jvm { get; init; } = [];
}
