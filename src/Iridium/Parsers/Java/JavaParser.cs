using System.Diagnostics;
using Iridium.Models.Java;

namespace Iridium.Parsers.Java;

public sealed class JavaParser {
    public static async Task<JavaEntry?> GetJavaEntryAsync(string javaPath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            return null;

        var properties = await GetJavaPropertiesAsync(javaPath, cancellationToken);

        if (!properties.TryGetValue("java.specification.version", out var specification) ||
            !properties.TryGetValue("java.version", out var version) ||
            !properties.TryGetValue("java.vendor", out var vendor) ||
            !properties.TryGetValue("java.home", out var home))
            return null;

        var major = ParseMajorVersion(specification);

        if (major == 0)
            return null;

        var compiler = OperatingSystem.IsWindows()
            ? "javac.exe"
            : "javac";

        var isJdk = File.Exists(Path.Combine(home, "bin", compiler));
        if (!isJdk) {
            var parent = Directory.GetParent(home)?.FullName;
            if (parent != null)
                isJdk = File.Exists(Path.Combine(parent, "bin", compiler));
        }

        return new JavaEntry {
            JavaPath = Path.GetFullPath(javaPath),
            JavaHome = home,
            Version = version,
            Vendor = vendor,
            MajorVersion = major,
            IsJdk = isJdk,
            Is64Bit = properties.TryGetValue("sun.arch.data.model", out var bit) &&
                      bit == "64"
        };
    }
    
    private static async ValueTask<Dictionary<string, string>> GetJavaPropertiesAsync(string javaPath, CancellationToken cancellationToken) {
        using var process = Process.Start(new ProcessStartInfo {
            FileName = javaPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            ArgumentList = {
                "-XshowSettings:properties",
                "-version"
            },
        });

        if (process is null) return [];

        // A broken or non-Java executable must not hang the whole scan.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        string output;
        try {
            output = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            if (!process.HasExited)
                process.Kill(true);
            return [];
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in output.AsSpan().EnumerateLines()) {
            var span = line.Trim();
            var index = span.IndexOf('=');

            if (index <= 0) continue;

            var key = span[..index].Trim();
            var value = span[(index + 1)..].Trim();

            if (key.Length == 0 || value.Length == 0)
                continue;

            result[key.ToString()] = value.ToString();
        }

        return result;
    }
    
    private static int ParseMajorVersion(string version) {
        if (string.IsNullOrEmpty(version))
            return 0;
        
        if (version.StartsWith("1.", StringComparison.Ordinal)) {

            if (version.Length > 2 && char.IsDigit(version[2]))
                return version[2] - '0';
            
            return 0;
        }
        
        var dot = version.IndexOf('.');
        var major = dot >= 0 
            ? version[..dot] 
            : version;
        
        return int.TryParse(major, out var result)
            ? result
            : 0;
    }
}