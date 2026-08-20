using System.Runtime.InteropServices;

namespace Iridium.Helpers;

public static class PathHelper {
    public static string GetDefaultMinecraftPath() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            // Windows: %APPDATA%\.minecraft
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, ".minecraft");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            // macOS: ~/Library/Application Support/minecraft
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "minecraft");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            // Linux: ~/.minecraft
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".minecraft");
        }

        throw new PlatformNotSupportedException("Unsupported operating system");
    }
}