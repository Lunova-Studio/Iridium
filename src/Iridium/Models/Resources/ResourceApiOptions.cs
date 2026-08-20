using Iridium.Enums.Resources;
using Iridium.Interfaces.Resources;

namespace Iridium.Models.Resources;


public sealed class ResourceApiOptions {

    public IReadOnlyList<IResourceMirror> Mirrors { get; init; } = [];


    public ResourceDownloadMode Mode { get; init; } = ResourceDownloadMode.Auto;


    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);


    public int MaxRetryCount { get; init; } = 3;


    public string? CurseForgeApiKey { get; init; }


    public string? UserAgent { get; init; } = "Iridium";
}
