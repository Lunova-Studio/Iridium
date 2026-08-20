using Iridium.Interfaces.Resources;

namespace Iridium.Models.Resources;


public sealed record ResourceFileEntry : IResourceFileEntry {
    public string? FileName { get; init; }
    public string? Url { get; init; }
    public long Size { get; init; }
    public string? Sha1 { get; init; }
    public string? Sha512 { get; init; }
    public string? Md5 { get; init; }
    public bool IsPrimary { get; init; }
}
