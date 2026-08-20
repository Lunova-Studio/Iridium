using Iridium.Enums.Resources;

namespace Iridium.Extensions.Resources;

public static class ResourceTypeExtensions {

    public static string ToModrinthProjectType(this ResourceType type) => type switch {
        ResourceType.Mod => "mod",
        ResourceType.Modpack => "modpack",
        ResourceType.ResourcePack => "resourcepack",
        ResourceType.Shader => "shader",
        ResourceType.DataPack => "datapack",
        ResourceType.Plugin => "plugin",
        ResourceType.World => throw new NotSupportedException("Modrinth 不提供 World 资源类型。"),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };


    public static int? ToCurseForgeClassId(this ResourceType type) => type switch {
        ResourceType.Mod => 6,
        ResourceType.Modpack => 4471,
        ResourceType.ResourcePack => 12,
        ResourceType.Shader => 6552,
        ResourceType.DataPack => 6945,
        ResourceType.World => 17,
        ResourceType.Plugin => 5,
        _ => null
    };
}
