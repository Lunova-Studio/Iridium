namespace Iridium.Models.Minecraft;

public sealed record MinecraftArgument {
    public IReadOnlyList<string> Values { get; init; } = [];
    public IReadOnlyList<CompatibilityRule>? Rules { get; init; }
}
