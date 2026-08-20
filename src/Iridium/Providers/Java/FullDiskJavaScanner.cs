namespace Iridium.Providers.Java;

using System.Runtime.CompilerServices;

public static class FullDiskJavaScanner {
    private const int MaxDepth = 64;
    
    private static readonly HashSet<string> SkipDirectoryNames = [
        "proc",
        "sys",
        "dev",
        "$Recycle.Bin",
        "System Volume Information",
        "tmp",
        "temp",
        "Temp",
        "node_modules",
        ".git",
        ".svn",
        ".hg",
        "__pycache__",
        "shadercache",
        "CrashDumps",
        ".gradle",
        ".m2",
        ".idea",
        "jetbrains-toolbox",
        "JetBrains"
    ];
    
    private static readonly EnumerationOptions EnumerationOptions = new() {
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
    
    private static string JavaExecutable =>
        OperatingSystem.IsWindows()
            ? "java.exe"
            : "java";

    public static async IAsyncEnumerable<string> ScanAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        foreach (var root in GetRoots()) {
            if (!Directory.Exists(root))
                continue;
            
            await foreach (var java in ScanDirectoryAsync(new DirectoryInfo(root), 0, cancellationToken))
                yield return java;
        }
    }

    private static IEnumerable<string> GetRoots() {
        if (OperatingSystem.IsWindows()) {
            foreach (var drive in DriveInfo.GetDrives()) {
                if (!drive.IsReady)
                    continue;
                
                if (drive.DriveType is DriveType.CDRom or DriveType.Network)
                    continue;
                
                yield return drive.RootDirectory.FullName;
            }

            yield break;
        }
        
        if (OperatingSystem.IsLinux()) {
            yield return "/usr";
            yield return "/home";
            yield return "/opt";

            yield break;
        }

        if (OperatingSystem.IsMacOS()) {
            yield return "/Applications";
            yield return "/Users";
            yield return "/opt";
            yield return "/usr/local";
        }
    }
    
    private static async IAsyncEnumerable<string> ScanDirectoryAsync(
        DirectoryInfo directory,
        int depth,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (depth > MaxDepth)
            yield break;
        
        IEnumerable<string> entries;
        try {
            entries = Directory.EnumerateFileSystemEntries(directory.FullName, "*", EnumerationOptions);
        } catch (UnauthorizedAccessException) {
            yield break;
        } catch (IOException) {
            yield break;
        }
        
        foreach (var entry in entries) {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(entry);
            
            if (string.IsNullOrEmpty(name))
                continue;
            
            if (File.Exists(entry)) {
                if (name.Equals(JavaExecutable, StringComparison.OrdinalIgnoreCase))
                    yield return entry;

                continue;
            }
            
            if (!Directory.Exists(entry))
                continue;
            
            if (SkipDirectoryNames.Contains(name))
                continue;

            await foreach (var java in ScanDirectoryAsync(new DirectoryInfo(entry), depth + 1, cancellationToken))
                yield return java;
        }
    }
}