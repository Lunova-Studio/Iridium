using Iridium.Enums.Resources;
using Iridium.Interfaces.Resources;
using Iridium.Models.Resources;

namespace Iridium.Services.Resources;





public static class ResourceFileGrouper {

    public static ResourceFileGrouping Group(IReadOnlyList<IResourceFile> files, bool groupByMajorVersion = false) {
        var unversioned = new List<IResourceFile>();
        var versionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files) {
            if (file.GameVersions.Count == 0) {
                unversioned.Add(file);
                continue;
            }

            foreach (var version in file.GameVersions) {
                var key = groupByMajorVersion ? GetMajorVersion(version) : version;
                if (!string.IsNullOrWhiteSpace(key))
                    versionKeys.Add(key);
            }
        }

        var versions = versionKeys
            .OrderByDescending(version => version, MinecraftVersionComparer.Instance)
            .Select(version => BuildVersionGroup(version, files, groupByMajorVersion))
            .ToArray();

        var recommended = versions
            .Where(group => group.Recommended?.Recommended is not null)
            .OrderByDescending(group => group.Recommended!.Recommended!.Published ?? DateTime.MinValue)
            .FirstOrDefault();

        return new ResourceFileGrouping {
            Versions = versions,
            Unversioned = unversioned,
            Recommended = recommended
        };
    }


    public static IReadOnlyList<IResourceFile> Filter(IReadOnlyList<IResourceFile> files,
        string? gameVersion = null,
        ResourceLoaderType loader = ResourceLoaderType.Any,
        ReleaseType releaseTypes = ReleaseType.All) {
        return files.Where(file =>
                (string.IsNullOrWhiteSpace(gameVersion) ||
                 file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase)) &&
                (loader == ResourceLoaderType.Any || file.Loaders.Contains(loader)) &&
                releaseTypes.HasFlag(file.ReleaseType))
            .ToArray();
    }


    public static IResourceFile? PickRecommended(IEnumerable<IResourceFile> files) =>
        files.OrderBy(file => ReleaseRank(file.ReleaseType))
            .ThenByDescending(file => file.Published ?? DateTime.MinValue)
            .FirstOrDefault();


    public static string GetMajorVersion(string gameVersion) {
        var parts = gameVersion.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : gameVersion;
    }

    private static ResourceVersionGroup BuildVersionGroup(string version,
        IReadOnlyList<IResourceFile> files, bool groupByMajorVersion) {
        var versionFiles = files
            .Where(file => file.GameVersions.Any(gameVersion => {
                var key = groupByMajorVersion ? GetMajorVersion(gameVersion) : gameVersion;
                return string.Equals(key, version, StringComparison.OrdinalIgnoreCase);
            }))
            .ToList();

        var loaderGroups = new List<ResourceLoaderGroup>();
        var loaderKeys = versionFiles.SelectMany(file => file.Loaders).Distinct().ToList();
        var anyLoaderFiles = versionFiles.Where(file => file.Loaders.Count == 0).ToList();

        foreach (var loader in loaderKeys) {
            var loaderFiles = versionFiles.Where(file => file.Loaders.Contains(loader)).ToList();
            if (loaderFiles.Count == 0)
                continue;
            loaderGroups.Add(BuildLoaderGroup(loader, loaderFiles));
        }

        if (anyLoaderFiles.Count > 0)
            loaderGroups.Add(BuildLoaderGroup(ResourceLoaderType.Any, anyLoaderFiles));

        var recommended = loaderGroups
            .Where(group => group.Recommended is not null)
            .OrderByDescending(group => group.Recommended!.Published ?? DateTime.MinValue)
            .FirstOrDefault();

        return new ResourceVersionGroup {
            GameVersion = version,
            Loaders = loaderGroups,
            Recommended = recommended
        };
    }

    private static ResourceLoaderGroup BuildLoaderGroup(ResourceLoaderType loader,
        IReadOnlyList<IResourceFile> files) {
        var ordered = files.OrderByDescending(file => file.Published ?? DateTime.MinValue).ToArray();
        return new ResourceLoaderGroup {
            Loader = loader,
            Files = ordered,
            Recommended = PickRecommended(ordered)
        };
    }

    private static int ReleaseRank(ReleaseType type) => type switch {
        ReleaseType.Release => 0,
        ReleaseType.Beta => 1,
        _ => 2
    };

    private sealed class MinecraftVersionComparer : IComparer<string> {
        public static readonly MinecraftVersionComparer Instance = new();

        public int Compare(string? left, string? right) {
            var leftParts = ParseVersion(left);
            var rightParts = ParseVersion(right);
            var length = Math.Max(leftParts.Length, rightParts.Length);
            for (var index = 0; index < length; index++) {
                var leftValue = index < leftParts.Length ? leftParts[index] : 0;
                var rightValue = index < rightParts.Length ? rightParts[index] : 0;
                if (leftValue != rightValue)
                    return leftValue.CompareTo(rightValue);
            }

            return 0;
        }

        private static int[] ParseVersion(string? version) {
            if (string.IsNullOrWhiteSpace(version))
                return [];
            return version.Split('.', '-')
                .TakeWhile(segment => int.TryParse(segment, out _))
                .Select(int.Parse)
                .ToArray();
        }
    }
}
