using Iridium.Enums.Resources;

namespace Iridium.Interfaces.Resources;


public interface IResourceDependency {
    string? ProjectId { get; }
    string? VersionId { get; }
    string? FileName { get; }
    DependencyType Type { get; }
}
