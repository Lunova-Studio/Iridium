using Iridium.Models.Download;

namespace Iridium.Download;

public sealed class DownloadSource {
    private readonly Func<DownloadFileEntry, string> _urlBuilder;

    private DownloadSource(string name, Func<DownloadFileEntry, string> urlBuilder) {
        Name = name;
        _urlBuilder = urlBuilder;
    }

    public string Name { get; }

    public static DownloadSource Official { get; } = new("Official", file => file.Type switch {
        DownloadFileType.Library => $"https://libraries.minecraft.net/{file.LibraryPath}",
        DownloadFileType.ClientJar => $"https://piston-data.mojang.com/v1/objects/{file.Sha1}/client.jar",
        DownloadFileType.AssetIndex => $"https://piston-meta.mojang.com/v1/packages/{file.Sha1}/{file.LibraryPath}",
        DownloadFileType.Asset => $"https://resources.download.minecraft.net/{file.Hash![..2]}/{file.Hash}",
        _ => throw new NotSupportedException($"Unsupported file type: {file.Type}")
    });

    public static DownloadSource BmclApi { get; } = new("BmclApi", file => file.Type switch {
        DownloadFileType.Library => $"https://bmclapi2.bangbang93.com/maven/{file.LibraryPath}",
        DownloadFileType.ClientJar => $"https://bmclapi2.bangbang93.com/version/{file.VersionId}/clientjar",
        DownloadFileType.AssetIndex => $"https://bmclapi2.bangbang93.com/mc-object/{file.Sha1}/{file.LibraryPath}",
        DownloadFileType.Asset => $"https://bmclapi2.bangbang93.com/assets/{file.Hash![..2]}/{file.Hash}",
        _ => throw new NotSupportedException($"Unsupported file type: {file.Type}")
    });

    public static DownloadSource Create(string name, Func<DownloadFileEntry, string> urlBuilder)
        => new(name, urlBuilder);

    public string GetUrl(DownloadFileEntry file) => _urlBuilder(file);
}