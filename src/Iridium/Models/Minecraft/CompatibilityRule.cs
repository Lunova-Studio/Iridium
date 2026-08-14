using Iridium.Enums;

namespace Iridium.Models.Minecraft;

public sealed record CompatibilityRule {
    public CompatibilityRuleAction Action { get; init; } = CompatibilityRuleAction.Allow;
    public string? OsName { get; init; }
    public string? OsVersion { get; init; }
    public string? OsArch { get; init; }
    public IReadOnlyDictionary<string, bool>? Features { get; init; }
}
