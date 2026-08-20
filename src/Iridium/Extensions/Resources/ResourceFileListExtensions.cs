using Iridium.Enums.Resources;
using Iridium.Interfaces.Resources;
using Iridium.Models.Resources;
using Iridium.Services.Resources;

namespace Iridium.Extensions.Resources;


public static class ResourceFileListExtensions {

    public static ResourceFileGrouping GroupByVersionAndLoader(this IReadOnlyList<IResourceFile> files,
        bool groupByMajorVersion = false) =>
        ResourceFileGrouper.Group(files, groupByMajorVersion);


    public static IReadOnlyList<IResourceFile> FilterFiles(this IReadOnlyList<IResourceFile> files,
        string? gameVersion = null,
        ResourceLoaderType loader = ResourceLoaderType.Any,
        ReleaseType releaseTypes = ReleaseType.All) =>
        ResourceFileGrouper.Filter(files, gameVersion, loader, releaseTypes);
}
