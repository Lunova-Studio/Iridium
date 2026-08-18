namespace Iridium.Models.Installation;

public sealed class InstallProgressChangedEventArgs : EventArgs {
    public IReadOnlyList<StepInfo> Steps { get; init; } = [];
    public double TotalProgress { get; init; }
    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
}