namespace Iridium.Models.Minecraft;

public sealed record MinecraftFileDownload {
    public string Url { get; init; } = string.Empty;
    public long Size { get; init; }
    public string? Sha1 { get; init; }
}
