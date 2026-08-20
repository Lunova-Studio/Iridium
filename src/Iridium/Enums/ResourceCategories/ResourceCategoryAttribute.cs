using Iridium.Enums.Resources;

namespace Iridium.Enums.ResourceCategories;


[AttributeUsage(AttributeTargets.Field)]
public sealed class CurseForgeCategoryAttribute(int categoryId) : Attribute {
    public int CategoryId { get; } = categoryId;
}


[AttributeUsage(AttributeTargets.Field)]
public sealed class ModrinthCategoryAttribute(string slug) : Attribute {
    public string Slug { get; } = slug;
}


[AttributeUsage(AttributeTargets.Enum)]
public sealed class ResourceCategoryTypeAttribute(ResourceType type) : Attribute {
    public ResourceType Type { get; } = type;
}
