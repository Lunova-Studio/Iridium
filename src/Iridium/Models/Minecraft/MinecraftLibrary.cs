namespace Iridium.Models.Minecraft;

public sealed record MinecraftLibrary {
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Absolute artifact download URL from the metadata (e.g. maven.minecraftforge.net).
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Maven-relative artifact path with forward slashes, when provided by the metadata.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Native classifier download URLs from downloads.classifiers, keyed by classifier
    /// name (e.g. natives-linux). Absent when the metadata provides no classifier info.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ClassifierUrls { get; init; }

    public IReadOnlyList<CompatibilityRule>? Rules { get; init; }
    public IReadOnlyDictionary<string, string>? Natives { get; init; }
}
