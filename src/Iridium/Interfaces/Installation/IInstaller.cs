using Iridium.Interfaces.Minecraft;
using Iridium.Models.Download;
using Iridium.Models.Installation;

namespace Iridium.Interfaces.Installation;

public interface IInstaller {
    event EventHandler<InstallerCompletedEventArgs>? Completed;
    event EventHandler<InstallProgressChangedEventArgs>? ProgressChanged;
    
    Task<DownloadResponse> InstallAsync(VersionManifestEntry id, IMinecraftLayout layout, CancellationToken cancellationToken = default);
}