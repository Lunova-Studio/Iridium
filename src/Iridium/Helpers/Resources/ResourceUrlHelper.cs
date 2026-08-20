using System.Text;
using Iridium.Enums.Resources;

namespace Iridium.Helpers.Resources;


public static class ResourceUrlHelper {

    public static string BuildCurseForgeCdnUrl(long fileId, string fileName) {
        var name = Uri.EscapeDataString(fileName);
        return $"https://edge.forgecdn.net/files/{fileId / 1000}/{fileId % 1000:D3}/{name}";
    }


    public static string CleanCurseForgeUrl(string url) =>
        url.Replace("-service.overwolf.wtf", ".forgecdn.net")
            .Replace("://media.", "://edge.")
            .Replace("://mediafilez.", "://edge.");


    public static string ApplyModrinthDownloadParams(string url, ModrinthDownloadReason reason,
        string? gameVersion = null, string? loader = null) {
        var builder = new StringBuilder(url);
        builder.Append(url.Contains('?') ? '&' : '?');
        builder.Append("mr_download_reason=").Append(reason switch {
            ModrinthDownloadReason.Standalone => "standalone",
            ModrinthDownloadReason.Dependency => "dependency",
            ModrinthDownloadReason.Modpack => "modpack",
            ModrinthDownloadReason.Update => "update",
            _ => "standalone"
        });
        if (!string.IsNullOrWhiteSpace(gameVersion))
            builder.Append("&mr_game_version=").Append(Uri.EscapeDataString(gameVersion));
        if (!string.IsNullOrWhiteSpace(loader))
            builder.Append("&mr_loader=").Append(Uri.EscapeDataString(loader));
        return builder.ToString();
    }


    public static string BuildModrinthWebsiteUrl(string projectType, string slug) =>
        $"https://modrinth.com/{projectType}/{slug}";


    public static string BuildCurseForgeWebsiteUrl(string slug) =>
        $"https://www.curseforge.com/minecraft/mc-mods/{slug}";
}
