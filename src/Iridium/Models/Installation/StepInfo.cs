namespace Iridium.Models.Installation;

public sealed record StepInfo {
    public double Progress { get; init; }
    
    public string Name { get; init; } = string.Empty;
}