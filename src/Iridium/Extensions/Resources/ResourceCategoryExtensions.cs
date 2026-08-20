using System.Reflection;
using Iridium.Enums.Resources;
using Iridium.Enums.ResourceCategories;
using Iridium.Models.Resources;

namespace Iridium.Extensions.Resources;


public static class ResourceCategoryExtensions {
    private static readonly Dictionary<Type, ResourceType> EnumTypeMap = new() {
        [typeof(ModCategory)] = ResourceType.Mod,
        [typeof(ModpackCategory)] = ResourceType.Modpack,
        [typeof(ResourcePackCategory)] = ResourceType.ResourcePack,
        [typeof(ShaderCategory)] = ResourceType.Shader,
        [typeof(DataPackCategory)] = ResourceType.DataPack,
        [typeof(WorldCategory)] = ResourceType.World
    };

    private static readonly Dictionary<ResourceType, Type> ReverseTypeMap =
        EnumTypeMap.ToDictionary(pair => pair.Value, pair => pair.Key);


    public static ResourceCategory ToResourceCategory<TEnum>(this TEnum value) where TEnum : struct, Enum {
        if (!EnumTypeMap.TryGetValue(typeof(TEnum), out var type))
            throw new ArgumentException($"不是静态分类枚举：{typeof(TEnum)}", nameof(value));
        return ToResourceCategory(typeof(TEnum), type, value);
    }


    public static IReadOnlyList<ResourceCategory> GetStaticCategories(this ResourceType type) {
        if (!ReverseTypeMap.TryGetValue(type, out var enumType))
            return [];

        return Enum.GetValues(enumType).Cast<object>()
            .Select(value => ToResourceCategory(enumType, type, value))
            .ToArray();
    }

    private static ResourceCategory ToResourceCategory(Type enumType, ResourceType type, object value) {
        var field = enumType.GetField(value.ToString()!)!;
        var curseForgeId = field.GetCustomAttribute<CurseForgeCategoryAttribute>()?.CategoryId;
        var modrinthSlug = field.GetCustomAttribute<ModrinthCategoryAttribute>()?.Slug;

        return new ResourceCategory {
            Type = type,
            Name = modrinthSlug ?? (curseForgeId.HasValue ? curseForgeId.Value.ToString() : value.ToString()!),
            DisplayName = value.ToString(),
            CurseForgeId = curseForgeId,
            ModrinthSlug = modrinthSlug
        };
    }
}
