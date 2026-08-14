using Iridium.Enums;

namespace Iridium.Models.Minecraft;

public sealed record MinecraftEntry {
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string InstancePath { get; init; } = string.Empty;
    public string MinecraftVersion { get; init; } = string.Empty;

    public MinecraftFormat Format { get; init; }

    public AssetIndex? AssetIndex { get; init; }
    public MinecraftArguments? Arguments { get; init; }
    public IReadOnlyList<MinecraftLoader> Loaders { get; init; } = [];
    public IReadOnlyList<MinecraftLibrary> Libraries { get; init; } = [];

    public string? Jar { get; init; }
    public string? MainClass { get; init; }
    public string? MinecraftArguments { get; init; }
    public string? InheritsFrom { get; init; }

    public MinecraftVersionType Type { get; init; }
    public DateTime? ReleaseTime { get; init; }
    public IReadOnlyList<string> Tweakers { get; init; } = [];
}
