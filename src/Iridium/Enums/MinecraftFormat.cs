using System.Runtime.InteropServices;

namespace Iridium.Enums;

public readonly struct MinecraftFormat(string id) : IEquatable<MinecraftFormat> {
    private readonly string _id = id;

    public static MinecraftFormat Prism { get; } = new("Prism");
    public static MinecraftFormat Standard { get; } = new("Standard");
    
    public bool Equals(MinecraftFormat other) =>
        string.Equals(_id, other._id, StringComparison.Ordinal);
    
    public static MinecraftFormat Create(string id) => new(id);
    
    public override string ToString() => _id;
    
    public override int GetHashCode() => _id?.GetHashCode() ?? 0;

    public override bool Equals(object? obj) => obj is MinecraftFormat other && Equals(other);
    
    public static bool operator !=(MinecraftFormat left, MinecraftFormat right) => !(left == right);
    public static bool operator ==(MinecraftFormat left, MinecraftFormat right) => left.Equals(right);
}