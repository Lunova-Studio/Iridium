using Iridium.Models.Installation;

namespace Iridium.Interfaces.Installation;

public interface IInstaller {
    event EventHandler<InstallerCompletedEventArgs>? Completed;
    event EventHandler<InstallProgressChangedEventArgs>? ProgressChanged;
    
    Task<MinecraftInstallResult> InstallAsync(CancellationToken cancellationToken = default);
}
