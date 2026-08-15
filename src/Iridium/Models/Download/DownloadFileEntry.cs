namespace Iridium.Models.Download;

public enum DownloadFileType {
    Library,
    ClientJar,
    AssetIndex,
    Asset
}

public sealed record DownloadFileEntry {
    public DownloadFileType Type { get; set; }

    public string? Sha1 { get; set; }
    public string Url { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;

    public long Size { get; set; }
    
    /// <summary>
    /// Maven relative path with forward slashes, e.g. "net/minecraft/launchwrapper/1.12/launchwrapper-1.12.jar".
    /// </summary>
    public string? LibraryPath { get; set; }

    /// <summary>
    /// 40-character hex asset hash.
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Version id used for client jar / asset index URLs.
    /// </summary>
    public string? VersionId { get; set; }
}