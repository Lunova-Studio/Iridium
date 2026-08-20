using Iridium.Enums.Resources;
using Iridium.Helpers.Resources;
using Iridium.Models.CurseForge;
using Iridium.Models.Modrinth;
using Iridium.Models.Resources;

namespace Iridium.Extensions.Resources;


public static class ResourceMappingExtensions {
    public static ResourceHit ToResourceHit(this ModrinthSearchHit hit, ResourceType type) {
        var slug = hit.Slug;
        return new ResourceHit {
            Source = ResourceSource.Modrinth,
            Id = hit.ProjectId ?? slug ?? string.Empty,
            Slug = slug,
            Title = hit.Title,
            Summary = hit.Description,
            IconUrl = hit.IconUrl,
            Author = hit.Author,
            Type = type,
            Downloads = hit.Downloads,
            Follows = hit.Follows,
            DateCreated = hit.DateCreated,
            DateModified = hit.DateModified,
            Categories = ToModrinthCategories(hit.Categories, type),
            GameVersions = hit.Versions,
            Screenshots = hit.Gallery,
            WebsiteUrl = slug is null ? null : BuildModrinthUrl(type, slug)
        };
    }

    public static ResourceHit ToResourceHit(this CurseForgeProject project, ResourceType type) {
        return new ResourceHit {
            Source = ResourceSource.CurseForge,
            Id = project.Id.ToString(),
            Slug = project.Slug,
            Title = project.Name,
            Summary = project.Summary,
            IconUrl = project.Logo?.ThumbnailUrl ?? project.Logo?.Url,
            Author = project.Authors.FirstOrDefault()?.Name,
            Type = type,
            Downloads = project.DownloadCount ?? 0,
            DateCreated = project.DateCreated,
            DateModified = project.DateModified,
            Categories = ToCurseForgeCategories(project.Categories, type),
            GameVersions = project.LatestFilesIndexes.Select(index => index.GameVersion)
                .Where(version => version is not null).Cast<string>().Distinct().ToArray(),
            Loaders = project.LatestFilesIndexes.Select(index => index.ModLoader.ParseCurseForgeLoader())
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = project.Screenshots.Select(screenshot => screenshot.Url ?? screenshot.ThumbnailUrl)
                .Where(url => url is not null).Cast<string>().ToArray(),
            WebsiteUrl = project.Links?.WebsiteUrl
        };
    }

    public static ResourceProject ToResourceProject(this ModrinthProject project) {
        var type = ParseProjectType(project.ProjectType);
        return new ResourceProject {
            Source = ResourceSource.Modrinth,
            Id = project.Id ?? string.Empty,
            Slug = project.Slug,
            Title = project.Title,
            Description = project.Description,
            Body = project.Body,
            IconUrl = project.IconUrl,
            Type = type,
            Downloads = project.Downloads,
            Follows = project.Followers,
            DateCreated = project.Published,
            DateModified = project.Updated,
            Categories = ToModrinthCategories(project.Categories, type),
            GameVersions = project.GameVersions,
            Loaders = project.Loaders.Select(loader => loader.ParseModrinthLoader())
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = project.Gallery.Select(gallery => gallery.Url).Where(url => url is not null).Cast<string>().ToArray(),
            LicenseId = project.License?.Id,
            WebsiteUrl = project.Slug is null ? null : BuildModrinthUrl(type, project.Slug)
        };
    }

    public static ResourceProject ToResourceProject(this CurseForgeProject project) {
        var type = ParseProjectType(project.ClassId ?? 0);
        return new ResourceProject {
            Source = ResourceSource.CurseForge,
            Id = project.Id.ToString(),
            Slug = project.Slug,
            Title = project.Name,
            Description = project.Summary,
            IconUrl = project.Logo?.ThumbnailUrl ?? project.Logo?.Url,
            Author = project.Authors.FirstOrDefault()?.Name,
            Type = type,
            Downloads = project.DownloadCount ?? 0,
            DateCreated = project.DateCreated,
            DateModified = project.DateModified,
            Categories = ToCurseForgeCategories(project.Categories, type),
            GameVersions = project.LatestFilesIndexes.Select(index => index.GameVersion)
                .Where(version => version is not null).Cast<string>().Distinct().ToArray(),
            Loaders = project.LatestFilesIndexes.Select(index => index.ModLoader.ParseCurseForgeLoader())
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = project.Screenshots.Select(screenshot => screenshot.Url ?? screenshot.ThumbnailUrl)
                .Where(url => url is not null).Cast<string>().ToArray(),
            WebsiteUrl = project.Links?.WebsiteUrl
        };
    }

    public static ResourceFile ToResourceFile(this ModrinthVersion version) {
        var primary = version.Files.FirstOrDefault(file => file.Primary) ?? version.Files.FirstOrDefault();
        return new ResourceFile {
            Source = ResourceSource.Modrinth,
            Id = version.Id ?? string.Empty,
            ProjectId = version.ProjectId ?? string.Empty,
            Name = version.Name,
            VersionNumber = version.VersionNumber,
            Changelog = version.Changelog,
            ReleaseType = ParseModrinthReleaseType(version.VersionType),
            Published = version.DatePublished,
            Downloads = version.Downloads,
            GameVersions = version.GameVersions,
            Loaders = version.Loaders.Select(loader => loader.ParseModrinthLoader())
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).ToArray(),
            PrimaryFile = primary?.ToResourceFileEntry(),
            Files = version.Files.Select(file => file.ToResourceFileEntry()).ToArray(),
            Dependencies = version.Dependencies.Select(dependency => dependency.ToResourceDependency()).ToArray()
        };
    }

    public static ResourceFile ToResourceFile(this CurseForgeFile file) {
        return new ResourceFile {
            Source = ResourceSource.CurseForge,
            Id = file.Id.ToString(),
            ProjectId = file.ModId?.ToString() ?? string.Empty,
            Name = file.DisplayName,
            VersionNumber = file.DisplayName,
            ReleaseType = ParseCurseForgeReleaseType(file.ReleaseType),
            Published = file.FileDate,
            Downloads = file.DownloadCount ?? 0,
            GameVersions = file.GameVersions,
            Loaders = file.GameVersions.Select(version => version.ParseModrinthLoader())
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            PrimaryFile = file.ToResourceFileEntry(),
            Files = [file.ToResourceFileEntry()],
            Dependencies = file.Dependencies.Select(dependency => dependency.ToResourceDependency()).ToArray()
        };
    }

    public static ResourceFileEntry ToResourceFileEntry(this ModrinthFile file) {
        return new ResourceFileEntry {
            FileName = file.FileName,
            Url = file.Url,
            Size = file.Size,
            Sha1 = file.Hashes?.Sha1,
            Sha512 = file.Hashes?.Sha512,
            IsPrimary = file.Primary
        };
    }

    public static ResourceFileEntry ToResourceFileEntry(this CurseForgeFile file) {
        var sha1 = file.Hashes.FirstOrDefault(hash => hash.Algo == 1)?.Value;
        var md5 = file.Hashes.FirstOrDefault(hash => hash.Algo == 2)?.Value;
        var url = file.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(file.FileName))
            url = ResourceUrlHelper.BuildCurseForgeCdnUrl(file.Id, file.FileName);

        return new ResourceFileEntry {
            FileName = file.FileName,
            Url = url,
            Size = file.FileLength ?? 0,
            Sha1 = sha1,
            Md5 = md5,
            IsPrimary = true
        };
    }

    public static ResourceDependency ToResourceDependency(this ModrinthDependency dependency) {
        return new ResourceDependency {
            ProjectId = dependency.ProjectId,
            VersionId = dependency.VersionId,
            FileName = dependency.FileName,
            Type = dependency.DependencyType?.ToLowerInvariant() switch {
                "required" => DependencyType.Required,
                "optional" => DependencyType.Optional,
                "embedded" => DependencyType.Embedded,
                "incompatible" => DependencyType.Incompatible,
                _ => DependencyType.Unknown
            }
        };
    }

    public static ResourceDependency ToResourceDependency(this CurseForgeDependency dependency) {
        return new ResourceDependency {
            ProjectId = dependency.ModId?.ToString() ?? string.Empty,
            Type = dependency.RelationType switch {
                1 => DependencyType.Embedded,
                2 => DependencyType.Optional,
                3 => DependencyType.Required,
                4 => DependencyType.Tool,
                5 => DependencyType.Incompatible,
                6 => DependencyType.Include,
                _ => DependencyType.Unknown
            }
        };
    }

    private static ResourceType ParseProjectType(string? modrinthType) =>
        modrinthType?.ToLowerInvariant() switch {
            "modpack" => ResourceType.Modpack,
            "resourcepack" => ResourceType.ResourcePack,
            "shader" => ResourceType.Shader,
            "datapack" => ResourceType.DataPack,
            "plugin" => ResourceType.Plugin,
            _ => ResourceType.Mod
        };

    private static ResourceType ParseProjectType(int curseForgeClassId) =>
        curseForgeClassId switch {
            4471 => ResourceType.Modpack,
            12 => ResourceType.ResourcePack,
            6552 => ResourceType.Shader,
            6945 => ResourceType.DataPack,
            17 => ResourceType.World,
            5 => ResourceType.Plugin,
            _ => ResourceType.Mod
        };

    private static ReleaseType ParseModrinthReleaseType(string? type) =>
        type?.ToLowerInvariant() switch {
            "beta" => ReleaseType.Beta,
            "alpha" => ReleaseType.Alpha,
            _ => ReleaseType.Release
        };

    private static ReleaseType ParseCurseForgeReleaseType(int? type) =>
        type switch {
            2 => ReleaseType.Beta,
            3 => ReleaseType.Alpha,
            _ => ReleaseType.Release
        };

    private static ResourceCategory[] ToModrinthCategories(IEnumerable<string> slugs, ResourceType type) =>
        slugs.Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => new ResourceCategory { Type = type, Name = slug!, ModrinthSlug = slug })
            .ToArray();

    private static ResourceCategory[] ToCurseForgeCategories(IEnumerable<CurseForgeCategory> categories,
        ResourceType type) =>
        categories.Select(category => new ResourceCategory {
            Type = type,
            Name = category.Slug ?? string.Empty,
            DisplayName = category.Name,
            CurseForgeId = category.Id
        }).ToArray();

    private static string BuildModrinthUrl(ResourceType type, string slug) =>
        ResourceUrlHelper.BuildModrinthWebsiteUrl(type.ToModrinthProjectType(), slug);
}
