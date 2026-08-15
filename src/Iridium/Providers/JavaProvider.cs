using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Iridium.Extensions;
using Iridium.Models.Java;
using Microsoft.Win32;

namespace Iridium.Providers;

public sealed class JavaProvider {
    private static readonly HashSet<string> SystemDirNames = [
        "proc", "sys", "dev", "run", "snap", "boot", "tmp", "etc",
        "var", "opt", "usr", "bin", "sbin", "lib", "lib32", "lib64",
        "System32", "SysWOW64", "Windows", "ProgramData", "Recovery",
        "$Recycle.Bin", "System Volume Information", "node_modules",
        ".git", ".svn", ".hg", "Library", ".Trash"
    ];

    public async IAsyncEnumerable<JavaEntry> EnumerableJavaAsync(
        bool fullDiskSearch = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var searched = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var entry in SearchPathAsync(searched, cancellationToken))
            yield return entry;

        await foreach (var entry in SearchEnvironmentVariablesAsync(searched, cancellationToken))
            yield return entry;

        await foreach (var entry in SearchJdksAsync(searched, cancellationToken))
            yield return entry;

        if (OperatingSystem.IsWindows())
            await foreach (var entry in SearchWindowsAsync(searched, fullDiskSearch, cancellationToken))
                yield return entry;

        if (OperatingSystem.IsLinux())
            await foreach (var entry in SearchLinuxAsync(searched, fullDiskSearch, cancellationToken))
                yield return entry;

        if (OperatingSystem.IsMacOS())
            await foreach (var entry in SearchMacOsAsync(searched, fullDiskSearch, cancellationToken))
                yield return entry;
    }

    public async Task<JavaEntry?> GetJavaEntryAsync(string javaPath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            return null;

        var props = await GetJavaPropertiesAsync(javaPath, cancellationToken);

        if (!props.TryGetValue("java.specification.version", out var spec) ||
            !props.TryGetValue("java.home", out var home) ||
            !props.TryGetValue("java.version", out var version) ||
            !props.TryGetValue("java.vendor", out var vendor))
            return null;

        var major = ParseMajorVersion(spec);
        if (major == 0)
            return null;

        var javacName = OperatingSystem.IsWindows() ? "javac.exe" : "javac";
        var isJdk = File.Exists(Path.Combine(home, "bin", javacName));

        if (!isJdk) {
            var parent = Directory.GetParent(home)?.FullName;
            if (parent is not null)
                isJdk = File.Exists(Path.Combine(parent, "bin", javacName));
        }

        var is64Bit = props.TryGetValue("sun.arch.data.model", out var bits) && bits == "64";

        return new JavaEntry {
            JavaPath = Path.GetFullPath(javaPath),
            JavaHome = home,
            IsJdk = isJdk,
            Is64Bit = is64Bit,
            Version = version,
            MajorVersion = major,
            Vendor = vendor
        };
    }

    private static string ResolveRealPath(string path) {
        try {
            var target = File.ResolveLinkTarget(path, true);
            return target?.FullName ?? path;
        } catch (IOException) {
            return path;
        } catch (UnauthorizedAccessException) {
            return path;
        }
    }

    private async ValueTask<JavaEntry?> TryAddJavaAsync(
        string javaPath,
        HashSet<string> searched,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(javaPath))
            return null;

        var realPath = ResolveRealPath(javaPath);

        if (!File.Exists(realPath) || !searched.Add(realPath))
            return null;

        return await GetJavaEntryAsync(realPath, cancellationToken);
    }

    private async IAsyncEnumerable<JavaEntry> ScanDirectoryForJavaAsync(
        string? baseDir,
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(baseDir))
            if (await TryAddJavaAsync(Path.Combine(dir, "bin", "java"), searched, cancellationToken) is { } entry)
                yield return entry;
    }

    private async IAsyncEnumerable<JavaEntry> SearchPathAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null)
            yield break;

        foreach (var dir in pathEnv.Split(Path.PathSeparator)) {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                continue;

            if (await TryAddJavaAsync(Path.Combine(dir, "java"), searched, cancellationToken) is { } entry)
                yield return entry;
        }
    }

    private async IAsyncEnumerable<JavaEntry> SearchEnvironmentVariablesAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (javaHome is not null)
            if (await TryAddJavaAsync(Path.Combine(javaHome, "bin", "java"), searched, cancellationToken) is { } entry)
                yield return entry;

        var hmclJres = Environment.GetEnvironmentVariable("HMCL_JRES");
        if (hmclJres is null)
            yield break;

        foreach (var home in hmclJres.Split(Path.PathSeparator)) {
            if (string.IsNullOrWhiteSpace(home) || !Directory.Exists(home))
                continue;

            if (await TryAddJavaAsync(Path.Combine(home, "bin", "java"), searched, cancellationToken) is { } entry)
                yield return entry;
        }
    }

    private async IAsyncEnumerable<JavaEntry> SearchJdksAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userHome))
            yield break;
        
        var jdksDir = Path.Combine(userHome, ".jdks");
        if (!Directory.Exists(jdksDir))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(jdksDir))
            if (await TryAddJavaAsync(Path.Combine(dir, "bin", "java"), searched, cancellationToken) is { } entry)
                yield return entry;
    }

    private async IAsyncEnumerable<JavaEntry> SearchHmclAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userHome))
            yield break;

        var hmclDir = Path.Combine(userHome, ".local", "share", "hmcl", "java");
        if (!Directory.Exists(hmclDir))
            yield break;

        foreach (var javaFile in new DirectoryInfo(hmclDir).FindAll("java"))
            if (await TryAddJavaAsync(javaFile.FullName, searched, cancellationToken) is { } entry)
                yield return entry;
    }

    private async IAsyncEnumerable<JavaEntry> SearchMinecraftRuntimeAsync(
        HashSet<string> searched,
        string runtimePath,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        if (!Directory.Exists(runtimePath))
            yield break;

        foreach (var javaFile in new DirectoryInfo(runtimePath).FindAll("java"))
            if (await TryAddJavaAsync(javaFile.FullName, searched, cancellationToken) is { } entry)
                yield return entry;
    }

    [SupportedOSPlatform("linux")]
    private async IAsyncEnumerable<JavaEntry> SearchLinuxAsync(
        HashSet<string> searched,
        bool fullDiskSearch,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        // Standard Java installation directories
        foreach (var dir in new[] { "/usr/java", "/usr/lib/jvm", "/usr/lib32/jvm", "/usr/lib64/jvm" })
            await foreach (var entry in ScanDirectoryForJavaAsync(dir, searched, cancellationToken))
                yield return entry;

        // SDKMAN!
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userHome)) {
            var sdkmanDir = Path.Combine(userHome, ".sdkman", "candidates", "java");
            await foreach (var entry in ScanDirectoryForJavaAsync(sdkmanDir, searched, cancellationToken))
                yield return entry;
        }

        // HMCL & Minecraft
        await foreach (var entry in SearchHmclAsync(searched, cancellationToken))
            yield return entry;

        if (!string.IsNullOrEmpty(userHome))
            await foreach (var entry in SearchMinecraftRuntimeAsync(searched,
                Path.Combine(userHome, ".minecraft", "runtime"), cancellationToken))
                yield return entry;

        if (fullDiskSearch)
            await foreach (var entry in SearchFullDiskAsync("/", searched, cancellationToken))
                yield return entry;
    }

    [SupportedOSPlatform("osx")]
    private async IAsyncEnumerable<JavaEntry> SearchMacOsAsync(
        HashSet<string> searched,
        bool fullDiskSearch,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var baseDir in new[] { "/Library/Java/JavaVirtualMachines",
            !string.IsNullOrEmpty(userHome) ? Path.Combine(userHome, "Library", "Java", "JavaVirtualMachines") : null }) {
            if (baseDir is null)
                continue;

            await foreach (var entry in ScanDirectoryForJavaAsync(baseDir, searched, cancellationToken))
                yield return entry;
        }

        var paths = new[] {
            "/opt/homebrew/opt/java/bin/java",
            "/usr/local/opt/java/bin/java", 
        };
        
        foreach (var path in paths)
            if (await TryAddJavaAsync(path, searched, cancellationToken) is { } entry)
                yield return entry;

        // Homebrew Cellar
        foreach (var cellarRoot in new[] { "/opt/homebrew/Cellar", "/usr/local/Cellar" }) {
            if (!Directory.Exists(cellarRoot))
                continue;

            foreach (var dir in new DirectoryInfo(cellarRoot).EnumerateDirectories("openjdk*"))
                if (await TryAddJavaAsync(Path.Combine(dir.FullName, "bin", "java"), searched, cancellationToken) is { } entry)
                    yield return entry;
        }

        // HMCL & Minecraft
        await foreach (var entry in SearchHmclAsync(searched, cancellationToken))
            yield return entry;

        if (!string.IsNullOrEmpty(userHome))
            await foreach (var entry in SearchMinecraftRuntimeAsync(searched,
                Path.Combine(userHome, "Library", "Application Support", "minecraft", "runtime"), cancellationToken))
                yield return entry;

        if (fullDiskSearch)
            await foreach (var entry in SearchFullDiskAsync("/", searched, cancellationToken))
                yield return entry;
    }

    [SupportedOSPlatform("Windows")]
    private async IAsyncEnumerable<JavaEntry> SearchWindowsAsync(
        HashSet<string> searched,
        bool fullDiskSearch,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        // where.exe
        await foreach (var entry in SearchWindowsWhereAsync(searched, cancellationToken))
            yield return entry;

        // Registry
        await foreach (var entry in SearchWindowsRegistryAsync(searched, cancellationToken))
            yield return entry;

        // Standard folders
        await foreach (var entry in SearchWindowsFoldersAsync(searched, cancellationToken))
            yield return entry;

        // HMCL
        await foreach (var entry in SearchHmclAsync(searched, cancellationToken))
            yield return entry;

        // Minecraft runtime
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (!string.IsNullOrEmpty(appData))
            await foreach (var entry in SearchMinecraftRuntimeAsync(searched, 
                               Path.Combine(appData, ".minecraft", "runtime"), cancellationToken))
                yield return entry;

        if (fullDiskSearch)
            await foreach (var entry in SearchFullDiskAsync(Environment.SystemDirectory[..2], searched, cancellationToken))
                yield return entry;
    }

    [SupportedOSPlatform("Windows")]
    private async IAsyncEnumerable<JavaEntry> SearchWindowsWhereAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        using var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "where.exe",
                ArgumentList = { "javaw.exe" },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        process.Start();

        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            if (line.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase) &&
                await TryAddJavaAsync(line, searched, cancellationToken) is { } entry)
                yield return entry;

        await process.WaitForExitAsync(cancellationToken);
    }

    [SupportedOSPlatform("Windows")]
    private async IAsyncEnumerable<JavaEntry> SearchWindowsRegistryAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var javaHomePaths = new List<string>();

        using var root = Registry.LocalMachine.OpenSubKey("SOFTWARE");
        if (root is not null) {
            CollectJavaHome(root, "JavaSoft", javaHomePaths);
            CollectJavaHome(root, "WOW6432Node\\JavaSoft", javaHomePaths);
        }

        var javaFiles = javaHomePaths
            .Where(Directory.Exists)
            .SelectMany(home => new DirectoryInfo(home).FindAll("javaw.exe"));
        
        foreach (var javaFile in javaFiles)
            if (await TryAddJavaAsync(javaFile.FullName, searched, cancellationToken) is { } entry)
                yield return entry;

        yield break;

        static void CollectJavaHome(RegistryKey key, string searchSubKey, List<string> results) {
            using var subKey = key.OpenSubKey(searchSubKey);
            if (subKey is null)
                return;

            // Search for JavaHome values recursively
            var queue = new Queue<RegistryKey>();
            queue.Enqueue(subKey);

            while (queue.Count > 0) {
                using var current = queue.Dequeue();

                foreach (var valueName in current.GetValueNames()) {
                    if (valueName == "JavaHome" && current.GetValue(valueName) is string home)
                        results.Add(home);
                }

                foreach (var name in current.GetSubKeyNames()) {
                    var child = current.OpenSubKey(name);
                    if (child is not null)
                        queue.Enqueue(child);
                }
            }
        }
    }

    [SupportedOSPlatform("Windows")]
    private async IAsyncEnumerable<JavaEntry> SearchWindowsFoldersAsync(
        HashSet<string> searched,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var folders = new[] {
            appData is not null ? Path.Combine(appData, ".minecraft", "cache", "java") : null,
            appData is not null ? Path.Combine(appData, ".minecraft", "runtime") : null,
            javaHome,
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFilesX86, "Java"),
            Path.Combine(programFiles, "Zulu"),
            Path.Combine(programFilesX86, "Zulu"),
        };
        
        foreach (var folder in folders) {
            if (folder is null || !Directory.Exists(folder))
                continue;

            foreach (var javaFile in new DirectoryInfo(folder).FindAll("javaw.exe"))
                if (await TryAddJavaAsync(javaFile.FullName, searched, cancellationToken) is { } entry)
                    yield return entry;
        }
    }

    private async IAsyncEnumerable<JavaEntry> SearchFullDiskAsync(string root, HashSet<string> searched, [EnumeratorCancellation] CancellationToken cancellationToken) {
        var pending = new Queue<string>();
        pending.Enqueue(root);

        for (var depth = 0; pending.Count > 0 && depth <= 8; depth++) {
            var count = pending.Count;
            while (count-- > 0) {
                cancellationToken.ThrowIfCancellationRequested();

                var dir = pending.Dequeue();

                if (await TryAddJavaAsync(Path.Combine(dir, "java"), searched, cancellationToken) is { } entry)
                    yield return entry;

                string[] children;
                try {
                    children = Directory.GetDirectories(dir);
                } catch {
                    continue;
                }

                foreach (var child in children) {
                    var name = Path.GetFileName(child);

                    if (name.StartsWith('.') && depth > 0)
                        continue;
                    if (SystemDirNames.Contains(name))
                        continue;

                    pending.Enqueue(child);
                }
            }
        }
    }

    private static async Task<Dictionary<string, string>> GetJavaPropertiesAsync(string javaPath, CancellationToken cancellationToken) {
        using var process = Process.Start(new ProcessStartInfo {
            FileName = javaPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            ArgumentList = { "-XshowSettings:properties", "-version" }
        });

        if (process is null)
            return [];

        var output = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var span = output.AsSpan();

        foreach (var lineRange in span.EnumerateLines()) {
            var line = lineRange.Trim();
            if (line.Length == 0)
                continue;

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (key.Length > 0 && value.Length > 0)
                result[key.ToString()] = value.ToString();
        }

        return result;
    }

    private static int ParseMajorVersion(string version) {
        // Java 8: "1.8.0_202" → "8"
        // Java 9+: "17.0.1" → "17"
        if (version.StartsWith("1.", StringComparison.Ordinal)) {
            if (version.Length > 2 && char.IsDigit(version[2]))
                return version[2] - '0';
            
            return 0;
        }

        var dot = version.IndexOf('.');
        var majorStr = dot >= 0 ? version[..dot] : version;
        
        return int.TryParse(majorStr, out var major) ? major : 0;
    }
}