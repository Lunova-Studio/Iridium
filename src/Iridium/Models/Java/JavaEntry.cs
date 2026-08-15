namespace Iridium.Models.Java;

public record JavaEntry {
    public string JavaPath { get; init; } = string.Empty;
    public string JavaHome { get; init; } = string.Empty;

    public string Vendor { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;

    public int MajorVersion { get; init; }

    public bool IsJdk { get; init; }
    public bool Is64Bit { get; init; }

    public override string ToString() => $"{Version} - {Vendor} - {JavaPath}";
}