namespace Iridium.Models.Installation;

public sealed class StepInfo {
    public string Name { get; }

    public double Weight { get; }

    public long TotalCount { get; internal set; } = 0L;
    public long CompletedCount { get; internal set; } = 0L;

    public double Progress => TotalCount > 0
        ? Math.Clamp(CompletedCount / (double)TotalCount, 0d, 1d) 
        : 0d;

    internal StepInfo(string name, double weight) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(weight, 0d);

        Name = name;
        Weight = weight;
    }
}