using System.Runtime.Versioning;
using Iridium.Helpers;
using Microsoft.Win32;

namespace Iridium.Providers.Java;

public static class FastJavaScanner {
    private static readonly string[] LinuxJavaRoots = [
        "/usr/lib/jvm",
        "/usr/lib32/jvm",
        "/usr/lib64/jvm",
        "/usr/java"
    ];

    private static readonly string[] MacOsRoots = [
        "/opt/homebrew/bin/java",
        "/usr/local/bin/java"
    ];

    private static readonly string[] MacOsJvmRoots = [
        "/Library/Java/JavaVirtualMachines"
    ];

    private static readonly string[] MacOsCellarRoots = [
        "/opt/homebrew/Cellar",
        "/usr/local/Cellar"
    ];

    private static readonly string[] WindowsJavaRoots = [
        "Java",
        "Zulu",
        "Microsoft",
        "Eclipse Adoptium"
    ];

    private static readonly string[] WindowsJavaRegistry = [
        @"SOFTWARE\JavaSoft",
        @"SOFTWARE\WOW6432Node\JavaSoft"
    ];

    private static readonly EnumerationOptions RecursiveOptions = new() {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private static readonly EnumerationOptions TopLevelOptions = new() {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private static string JavaExecutable =>
        OperatingSystem.IsWindows() ? "java.exe" : "java";

    public static IEnumerable<string> Scan() {
        foreach (var java in SearchEnvironment())
            yield return java;

        foreach (var java in SearchMinecraftRuntime())
            yield return java;

        foreach (var java in SearchHmcl())
            yield return java;

        foreach (var java in SearchUserJdks())
            yield return java;

        if (OperatingSystem.IsWindows())
            foreach (var java in SearchWindows())
                yield return java;
        else if (OperatingSystem.IsLinux())
            foreach (var java in SearchLinux())
                yield return java;
        else if (OperatingSystem.IsMacOS())
            foreach (var java in SearchMacOs())
                yield return java;
    }

    private static IEnumerable<string> SearchEnvironment() {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");

        if (!string.IsNullOrEmpty(javaHome)) {
            var java = CombineJava(javaHome);
            if (java is not null)
                yield return java;
        }

        var path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
            yield break;

        foreach (var directory in path.Split(Path.PathSeparator)) {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            var java = Path.Combine(directory, JavaExecutable);

            if (File.Exists(java))
                yield return java;
        }
    }

    private static IEnumerable<string> SearchMinecraftRuntime() {
        var minecraftPath = PathHelper.GetDefaultMinecraftPath();

        if (string.IsNullOrEmpty(minecraftPath))
            yield break;

        foreach (var java in SearchRecursive(Path.Combine(minecraftPath, "runtime")))
            yield return java;
    }

    private static IEnumerable<string> SearchHmcl() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            yield break;

        var path = OperatingSystem.IsWindows()
            ? Path.Combine(home, ".minecraft", "hmcl", "java")
            : Path.Combine(home, ".local", "share", "hmcl", "java");

        foreach (var java in SearchRecursive(path))
            yield return java;
    }

    private static IEnumerable<string> SearchUserJdks() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrEmpty(home))
            yield break;

        foreach (var java in SearchDirectory(Path.Combine(home, ".jdks")))
            yield return java;
    }

    private static IEnumerable<string> SearchLinux() {
        foreach (var root in LinuxJavaRoots) {
            foreach (var java in SearchDirectory(root))
                yield return java;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrEmpty(home))
            yield break;

        foreach (var java in SearchDirectory(Path.Combine(home, ".sdkman", "candidates", "java")))
            yield return java;
    }

    private static IEnumerable<string> SearchMacOs() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home)) {
            foreach (var java in SearchDirectory(Path.Combine(home, "Library", "Java", "JavaVirtualMachines")))
                yield return java;
        }

        foreach (var root in MacOsJvmRoots) {
            foreach (var java in SearchDirectory(root))
                yield return java;
        }

        foreach (var java in MacOsRoots)
            if (File.Exists(java))
                yield return java;

        // Homebrew Cellar layout: <formula>/<version>/bin/java
        foreach (var cellarRoot in MacOsCellarRoots) {
            if (!Directory.Exists(cellarRoot))
                continue;

            foreach (var formula in Directory.EnumerateDirectories(cellarRoot, "openjdk*", TopLevelOptions)) {
                foreach (var versionDir in Directory.EnumerateDirectories(formula, "*", TopLevelOptions)) {
                    if (CombineJava(versionDir) is { } java)
                        yield return java;
                }
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> SearchWindows() {
        foreach (var java in SearchRegistry())
            yield return java;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrEmpty(programFiles)) {
            foreach (var folder in WindowsJavaRoots) {
                foreach (var java in SearchDirectory(Path.Combine(programFiles, folder)))
                    yield return java;
            }
        }

        if (!string.IsNullOrEmpty(programFilesX86) &&
            !string.Equals(programFiles, programFilesX86, StringComparison.OrdinalIgnoreCase)) {
            foreach (var folder in WindowsJavaRoots) {
                foreach (var java in SearchDirectory(Path.Combine(programFilesX86, folder)))
                    yield return java;
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> SearchRegistry() {
        foreach (var path in WindowsJavaRegistry) {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key is null) continue;

            foreach (var version in key.GetSubKeyNames()) {
                using var versionKey = key.OpenSubKey(version);

                if (versionKey?.GetValue("JavaHome") is not string home)
                    continue;

                if (CombineJava(home) is { } java)
                    yield return java;
            }
        }
    }

    private static IEnumerable<string> SearchDirectory(string directory) {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var child in Directory.EnumerateDirectories(directory, "*", TopLevelOptions)) {
            if (CombineJava(child) is { } java)
                yield return java;
        }
    }

    private static IEnumerable<string> SearchRecursive(string directory) {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var java in Directory.EnumerateFiles(directory, JavaExecutable, RecursiveOptions))
            yield return java;
    }

    private static string? CombineJava(string directory) {
        // Skip symlinked directories; the real target is enumerated separately,
        // which also prevents duplicate entries.
        if (new DirectoryInfo(directory).LinkTarget is not null)
            return null;

        var java = Path.Combine(directory, "bin", JavaExecutable);
        if (File.Exists(java))
            return java;

        // macOS .jdk bundles keep the executable under Contents/Home.
        java = Path.Combine(directory, "Contents", "Home", "bin", JavaExecutable);
        return File.Exists(java) ? java : null;
    }
}
