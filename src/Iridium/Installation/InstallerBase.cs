using Iridium.Interfaces.Installation;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Download;
using Iridium.Models.Installation;

namespace Iridium.Installation;

public abstract class InstallerBase : IInstaller {
    public event EventHandler<InstallProgressChangedEventArgs>? ProgressChanged;
    public event EventHandler<InstallerCompletedEventArgs>? Completed;

    // public abstract string MinecraftFolder { get; }

    public abstract Task<DownloadResponse> InstallAsync(VersionManifestEntry id, IMinecraftLayout layout, CancellationToken cancellationToken = default);

    protected void ReportProgress(IReadOnlyList<string> stepNames, int completedSteps, int currentStepIndex, double currentStepProgress) {
        var totalSteps = stepNames.Count;
        var steps = new StepInfo[totalSteps];
        for (var i = 0; i < totalSteps; i++) {
            var progress = i < completedSteps ? 1.0 : i == currentStepIndex ? currentStepProgress : 0.0;
            steps[i] = new StepInfo { Name = stepNames[i], Progress = progress };
        }

        var totalProgress = (completedSteps + currentStepProgress) / totalSteps;
        if (totalProgress > 1.0)
            totalProgress = 1.0;

        ProgressChanged?.Invoke(this, new InstallProgressChangedEventArgs {
            Steps = steps,
            TotalProgress = totalProgress,
            CompletedSteps = completedSteps,
            TotalSteps = totalSteps
        });
    }

    protected void ReportCompleted(bool isSuccess, Exception? exception = null) {
        Completed?.Invoke(this, new InstallerCompletedEventArgs {
            IsSuccess = isSuccess,
            Exception = exception
        });
    }
}