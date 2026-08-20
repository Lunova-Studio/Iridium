namespace Iridium.Interfaces.Resources;


public interface IResourceFileEntry {
    string? FileName { get; }
    string? Url { get; }
    long Size { get; }
    string? Sha1 { get; }
    string? Sha512 { get; }
    string? Md5 { get; }
    bool IsPrimary { get; }
}
