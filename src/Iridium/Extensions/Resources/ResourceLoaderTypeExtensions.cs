using Iridium.Enums.Resources;

namespace Iridium.Extensions.Resources;

public static class ResourceLoaderTypeExtensions {

    public static string? ToModrinthLoader(this ResourceLoaderType loader) => loader switch {
        ResourceLoaderType.Vanilla => "vanilla",
        ResourceLoaderType.Forge => "forge",
        ResourceLoaderType.Fabric => "fabric",
        ResourceLoaderType.Quilt => "quilt",
        ResourceLoaderType.NeoForge => "neoforge",
        ResourceLoaderType.LiteLoader => "liteloader",
        ResourceLoaderType.OptiFine => "optifine",
        ResourceLoaderType.Canvas => "canvas",
        ResourceLoaderType.Iris => "iris",
        ResourceLoaderType.LegacyFabric => "legacy-fabric",
        ResourceLoaderType.Paper => "paper",
        ResourceLoaderType.Purpur => "purpur",
        ResourceLoaderType.Spigot => "spigot",
        ResourceLoaderType.Bukkit => "bukkit",
        ResourceLoaderType.Velocity => "velocity",
        ResourceLoaderType.Waterfall => "waterfall",
        ResourceLoaderType.BungeeCord => "bungeecord",
        _ => null
    };


    public static int? ToCurseForgeLoaderType(this ResourceLoaderType loader) => loader switch {
        ResourceLoaderType.Forge => 1,
        ResourceLoaderType.LiteLoader => 3,
        ResourceLoaderType.Fabric => 4,
        ResourceLoaderType.Quilt => 5,
        ResourceLoaderType.NeoForge => 6,
        ResourceLoaderType.Canvas => 8,
        ResourceLoaderType.Iris => 9,
        ResourceLoaderType.OptiFine => 10,
        ResourceLoaderType.Vanilla => 11,
        _ => null
    };


    public static ResourceLoaderType? ParseModrinthLoader(this string? loader) =>
        loader?.Trim().ToLowerInvariant() switch {
            "vanilla" => ResourceLoaderType.Vanilla,
            "forge" => ResourceLoaderType.Forge,
            "fabric" => ResourceLoaderType.Fabric,
            "quilt" => ResourceLoaderType.Quilt,
            "neoforge" => ResourceLoaderType.NeoForge,
            "liteloader" => ResourceLoaderType.LiteLoader,
            "optifine" => ResourceLoaderType.OptiFine,
            "canvas" => ResourceLoaderType.Canvas,
            "iris" => ResourceLoaderType.Iris,
            "legacy-fabric" => ResourceLoaderType.LegacyFabric,
            "paper" => ResourceLoaderType.Paper,
            "purpur" => ResourceLoaderType.Purpur,
            "spigot" => ResourceLoaderType.Spigot,
            "bukkit" => ResourceLoaderType.Bukkit,
            "velocity" => ResourceLoaderType.Velocity,
            "waterfall" => ResourceLoaderType.Waterfall,
            "bungeecord" => ResourceLoaderType.BungeeCord,
            _ => null
        };


    public static ResourceLoaderType? ParseCurseForgeLoader(this int? loader) => loader switch {
        1 => ResourceLoaderType.Forge,
        3 => ResourceLoaderType.LiteLoader,
        4 => ResourceLoaderType.Fabric,
        5 => ResourceLoaderType.Quilt,
        6 => ResourceLoaderType.NeoForge,
        8 => ResourceLoaderType.Canvas,
        9 => ResourceLoaderType.Iris,
        10 => ResourceLoaderType.OptiFine,
        11 => ResourceLoaderType.Vanilla,
        _ => null
    };
}
