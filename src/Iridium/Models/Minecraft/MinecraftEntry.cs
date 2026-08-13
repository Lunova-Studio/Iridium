using Iridium.Enums;

namespace Iridium.Models.Minecraft;

public sealed record MinecraftEntry {
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string InstancePath { get; init; } = string.Empty;
    public string MinecraftVersion { get; init; } = string.Empty;
    
    public MinecraftFormat Format { get; init; }
    
    public IReadOnlyList<MinecraftLoader> Loaders { get; init; } = [];
}
