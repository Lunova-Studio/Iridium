using Iridium.Interfaces.Installation;
using Iridium.Models.Installation;

namespace Iridium.Installation;

public abstract class InstallerBase : IInstaller {
    private double _totalWeight;
    private double _weightedProgress;

    public event EventHandler<InstallerCompletedEventArgs>? Completed;
    public event EventHandler<InstallProgressChangedEventArgs>? ProgressChanged;

    protected abstract StepInfo[] Steps { get; }
    
    public abstract Task<MinecraftInstallResult> InstallAsync(CancellationToken cancellationToken = default);

    protected void InitializeProgress()
    {
        _totalWeight = 0d;
        _weightedProgress = 0d;

        foreach (var step in Steps)
        {
            _totalWeight += step.Weight;
        }
    }

    protected void UpdateStep(
        int index,
        long completedCount,
        long totalCount)
    {
        var step = GetStep(index);

        var oldProgress = step.Progress;

        step.TotalCount = Math.Max(0L, totalCount);
        step.CompletedCount = Math.Clamp(
            completedCount,
            0L,
            step.TotalCount);

        var newProgress = step.Progress;

        _weightedProgress +=
            (newProgress - oldProgress) * step.Weight;

        ReportProgress();
    }

    protected void IncrementStep(
        int index,
        long count = 1)
    {
        if (count <= 0)
            return;

        var step = GetStep(index);

        var oldProgress = step.Progress;

        var newCompletedCount = step.CompletedCount + count;

        step.CompletedCount = Math.Min(
            newCompletedCount,
            step.TotalCount);

        var newProgress = step.Progress;

        _weightedProgress +=
            (newProgress - oldProgress) * step.Weight;

        ReportProgress();
    }

    protected void CompleteStep(int index)
    {
        var step = GetStep(index);

        var oldProgress = step.Progress;

        step.CompletedCount = step.TotalCount;

        var newProgress = step.Progress;

        _weightedProgress +=
            (newProgress - oldProgress) * step.Weight;

        ReportProgress();
    }

    protected StepInfo GetStep(int index)
    {
        if ((uint)index >= (uint)Steps.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return Steps[index];
    }

    protected void ReportProgress()
    {
        if (Steps.Length == 0)
            return;

        var totalProgress = _totalWeight > 0d
            ? _weightedProgress / _totalWeight
            : 0d;

        totalProgress = Math.Clamp(
            totalProgress,
            0d,
            1d);

        ProgressChanged?.Invoke(
            this,
            new InstallProgressChangedEventArgs(
                Steps,
                totalProgress));
    }

    protected void ReportCompleted(
        bool isSuccess,
        Exception? exception = null)
    {
        Completed?.Invoke(
            this,
            new InstallerCompletedEventArgs(
                isSuccess,
                exception));
    }
}