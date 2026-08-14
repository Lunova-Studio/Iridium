using System.Diagnostics;

namespace Iridium.Providers;

public sealed class JavaProvider
{
    public async Task<JavaEntry?> GetJavaEntryAsync(string javaPath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            return null;

        var p = await GetJavaPropertiesAsync(javaPath, cancellationToken);
        if (!p.TryGetValue("java.specification.version", out var spec) ||
            !p.TryGetValue("java.home", out var home) ||
            !p.TryGetValue("java.version", out var version) ||
            !p.TryGetValue("java.vendor", out var vendor))
            return null;

        var major = ParseMajorVersion(spec);
        if (major == 0)
            return null;

        var javac = OperatingSystem.IsWindows() ? "javac.exe" : "javac";
        var jdk = File.Exists(Path.Combine(home, "bin", javac))
            ? home
            : Directory.GetParent(home)?.FullName is { } parent &&
              File.Exists(Path.Combine(parent, "bin", javac))
                ? parent
                : null;

        var isJdk = jdk is not null;
        var is64Bit = p.TryGetValue("sun.arch.data.model", out var bits) && bits == "64";
        
        return new JavaEntry {
            JavaPath = Path.GetFullPath(javaPath),
            JavaHome = jdk ?? home,
            IsJdk = isJdk,
            Is64Bit = is64Bit,
            Version = version,
            MajorVersion = major,
            Vendor = vendor
        };
    }

    private static int ParseMajorVersion(string version) {
        var majorVersionStr = version.StartsWith("1.", StringComparison.Ordinal) 
            ? version[2..] 
            : version;
        
        return int.TryParse(majorVersionStr, out var majorVersion) 
            ? majorVersion 
            : 0;
    }

    private static async Task<IReadOnlyDictionary<string, string>> GetJavaPropertiesAsync(string javaPath, CancellationToken cancellationToken) {
        using var process = Process.Start(new ProcessStartInfo {
            FileName = javaPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            ArgumentList = { "-XshowSettings:properties", "-version" }
        });

        if (process is null)
            return new Dictionary<string, string>();

        var outputTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        
        var output = await outputTask.ConfigureAwait(false);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
            var parts = line.Split('=', 2);
            if (parts is not [var key, var value])
                continue;

            key = key.Trim();
            value = value.Trim();

            if (key.Length > 0 && value.Length > 0)
                result[key] = value;
        }

        return result;
    }
}

public record JavaEntry {
    public string JavaPath { get; init; } = string.Empty;
    public string JavaHome { get; init; } = string.Empty;

    public string Vendor { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;

    public int MajorVersion { get; init; }

    public bool IsJdk { get; init; }
    public bool Is64Bit { get; init; }

    public override string ToString() => $"{Version} - {Vendor} - {JavaPath}";
}

    // /// <summary>
    // /// 解析指定路径的JVM信息
    // /// </summary>
    // public static class JavaRuntimeParser
    // {
    //     private static OSPlatform Unknown = OSPlatform.Create("Unknown");
    //     
    //     /// <summary>
    //     /// 解析指定路径的JVM信息
    //     /// </summary>
    //     /// <param name="javaHome">JAVA_HOME路径，如 C:\Program Files\Java\jdk-17</param>
    //     /// <returns>Java运行时信息，如果无效返回null</returns>
    //     public static JavaRuntime? Parse(string javaHome)
    //     {
    //         if (string.IsNullOrEmpty(javaHome))
    //             return null;
    //
    //         if (!Directory.Exists(javaHome))
    //             return null;
    //
    //         // 检测操作系统
    //         var os = DetectOperatingSystem();
    //         string javaExecutable = os == OSPlatform.Windows ? "java.exe" : "java";
    //         string javacExecutable = os == OSPlatform.Windows ? "javac.exe" : "javac";
    //
    //         // 检查java可执行文件是否存在
    //         string javaPath = Path.Combine(javaHome, "bin", javaExecutable);
    //         if (!File.Exists(javaPath))
    //             return null;
    //
    //         // 获取版本信息
    //         var versionInfo = GetVersionInfo(javaPath);
    //         if (versionInfo == null)
    //             return null;
    //
    //         // 检测是否为JDK（检查javac是否存在）
    //         string javacPath = Path.Combine(javaHome, "bin", javacExecutable);
    //         bool isJdk = File.Exists(javacPath);
    //
    //         // 检测架构
    //         var arch = DetectArchitecture();
    //
    //         return new JavaRuntime(
    //             new FileInfo(javaPath),
    //             versionInfo.Version,
    //             versionInfo.Vendor,
    //             isJdk,
    //             arch
    //         );
    //     }
    //
    //     /// <summary>
    //     /// 获取Java版本信息
    //     /// </summary>
    //     private static JavaVersionInfo? GetVersionInfo(string javaPath)
    //     {
    //         try
    //         {
    //             var startInfo = new ProcessStartInfo
    //             {
    //                 FileName = javaPath,
    //                 Arguments = "-version",
    //                 RedirectStandardError = true,
    //                 RedirectStandardOutput = true,
    //                 UseShellExecute = false,
    //                 CreateNoWindow = true
    //             };
    //
    //             using var process = Process.Start(startInfo);
    //             if (process == null)
    //                 return null;
    //
    //             // java -version 输出到标准错误流
    //             string output = process.StandardError.ReadToEnd();
    //             process.WaitForExit(3000);
    //
    //             if (string.IsNullOrEmpty(output))
    //                 output = process.StandardOutput.ReadToEnd();
    //
    //             if (string.IsNullOrEmpty(output))
    //                 return null;
    //
    //             return ParseVersionOutput(output);
    //         }
    //         catch
    //         {
    //             return null;
    //         }
    //     }
    //
    //     /// <summary>
    //     /// 解析 java -version 输出
    //     /// </summary>
    //     private static JavaVersionInfo? ParseVersionOutput(string output) {
    //         var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    //         if (lines.Length == 0)
    //             return null;
    //
    //         string version = "unknown";
    //         string vendor = "unknown";
    //
    //         // 解析第一行获取版本
    //         var firstLine = lines[0].Trim();
    //         var versionMatch = Regex.Match(firstLine, @"version\s+""([^""]+)""");
    //         if (versionMatch.Success)
    //         {
    //             version = versionMatch.Groups[1].Value;
    //         }
    //         else
    //         {
    //             // 尝试其他格式
    //             var altMatch = Regex.Match(firstLine, @"(\d+\.\d+\.\d+[_\+\-\w]*)");
    //             if (altMatch.Success)
    //                 version = altMatch.Groups[1].Value;
    //         }
    //
    //         // 解析供应商
    //         if (firstLine.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase))
    //             vendor = "OpenJDK";
    //         else if (firstLine.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase))
    //             vendor = "Oracle";
    //         else if (firstLine.Contains("Eclipse", StringComparison.OrdinalIgnoreCase))
    //             vendor = "Eclipse";
    //         else if (firstLine.Contains("IBM", StringComparison.OrdinalIgnoreCase))
    //             vendor = "IBM";
    //         else if (firstLine.Contains("Azul", StringComparison.OrdinalIgnoreCase))
    //             vendor = "Azul";
    //
    //         // 解析主版本号
    //         int parsedVersion = ParseMajorVersion(version);
    //
    //         return new JavaVersionInfo(version, vendor, parsedVersion);
    //     }
    //
    //     /// <summary>
    //     /// 解析主版本号
    //     /// </summary>
    //     internal static int ParseMajorVersion(string version)
    //     {
    //         // Java 8 格式: 1.8.0_xxx
    //         if (version.StartsWith("1."))
    //         {
    //             if (version.Length >= 3 && char.IsDigit(version[2]))
    //                 return version[2] - '0';
    //             return -1;
    //         }
    //
    //         // Java 9+ 格式: 17.0.5
    //         var match = Regex.Match(version, @"^(\d+)");
    //         if (match.Success && int.TryParse(match.Groups[1].Value, out int major))
    //             return major;
    //
    //         return -1;
    //     }
    //
    //     /// <summary>
    //     /// 检测操作系统
    //     /// </summary>
    //     private static OSPlatform DetectOperatingSystem()
    //     {
    //         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    //             return OSPlatform.Windows;
    //         
    //         if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    //             return OSPlatform.Linux;
    //         
    //         if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    //             return OSPlatform.OSX;
    //         
    //         return Unknown;
    //     }
    //
    //     /// <summary>
    //     /// 检测架构
    //     /// </summary>
    //     private static string DetectArchitecture()
    //     {
    //         return RuntimeInformation.ProcessArchitecture.ToString();
    //     }
    //
    //     /// <summary>
    //     /// Java版本信息内部类
    //     /// </summary>
    //     private class JavaVersionInfo
    //     {
    //         public string Version { get; }
    //         public string Vendor { get; }
    //         public int ParsedVersion { get; }
    //
    //         public JavaVersionInfo(string version, string vendor, int parsedVersion)
    //         {
    //             Version = version;
    //             Vendor = vendor;
    //             ParsedVersion = parsedVersion;
    //         }
    //     }
    // }
    //
    // /// <summary>
    // /// Java运行时信息（纯数据模型）
    // /// </summary>
    // public class JavaRuntime
    // {
    //     public FileInfo Binary { get; }
    //     public string Version { get; }
    //     public string Vendor { get; }
    //     public bool IsJdk { get; }
    //     public string Architecture { get; }
    //     public int ParsedVersion { get; }
    //
    //     public JavaRuntime(FileInfo binary, string version, string vendor, bool isJdk, string architecture)
    //     {
    //         Binary = binary;
    //         Version = version;
    //         Vendor = vendor;
    //         IsJdk = isJdk;
    //         Architecture = architecture;
    //         ParsedVersion = JavaRuntimeParser.ParseMajorVersion(version);
    //     }
    //
    //     public override string ToString()
    //     {
    //         return $"Java {Version} ({(IsJdk ? "JDK" : "JRE")}) - {Binary.FullName}";
    //     }
    // }