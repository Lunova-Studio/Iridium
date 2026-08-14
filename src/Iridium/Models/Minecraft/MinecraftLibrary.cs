namespace Iridium.Models.Minecraft;

public sealed record MinecraftLibrary {
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<CompatibilityRule>? Rules { get; init; }
    public IReadOnlyDictionary<string, string>? Natives { get; init; }
}
