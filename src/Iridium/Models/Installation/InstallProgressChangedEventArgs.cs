namespace Iridium.Models.Installation;

public sealed class InstallProgressChangedEventArgs : EventArgs {
    public double TotalProgress { get; }

    public IReadOnlyList<StepInfo> Steps { get; }

    internal InstallProgressChangedEventArgs(IReadOnlyList<StepInfo> steps, double totalProgress) {
        Steps = steps;
        TotalProgress = totalProgress;
    }
}
