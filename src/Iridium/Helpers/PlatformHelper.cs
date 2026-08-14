using System.Runtime.InteropServices;

namespace Iridium.Helpers;

public static class PlatformHelper {
    public static Architecture Architecture => RuntimeInformation.ProcessArchitecture;
    
    public static string GetPlatformInfo() {
        var os = OperatingSystem.IsWindows() 
            ? "windows" 
            : OperatingSystem.IsMacOS() 
                ? "osx" 
                : "linux";
        
        var arch = Architecture switch {
            Architecture.X86 => "x86",
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant()
        };
        
        return $"{os}-{arch}";
    }
}