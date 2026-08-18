using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Iridium.Enums;
using Iridium.Models.Minecraft;

namespace Iridium.Launch;

internal static class VersionArgumentRuleParser {
    public static bool IsActive(IReadOnlyList<CompatibilityRule>? rules, Dictionary<string, bool> features) {
        if (rules is null || rules.Count == 0)
            return true;

        var allowed = false;
        foreach (var rule in rules) {
            if (!IsMatched(rule, features))
                continue;

            if (rule.Action == CompatibilityRuleAction.Disallow)
                return false;

            allowed = true;
        }

        return allowed;
    }

    private static bool IsMatched(CompatibilityRule rule, Dictionary<string, bool> features) {
        if (rule.OsName is not null &&
            !string.Equals(GetCurrentOsName(), rule.OsName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.OsVersion is not null &&
            !Regex.IsMatch(Environment.OSVersion.Version.ToString(), rule.OsVersion))
        {
            return false;
        }

        if (rule.OsArch is not null &&
            !string.Equals(GetCurrentOsArch(), rule.OsArch, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.Features is null)
            return true;

        foreach (var (key, value) in rule.Features) {
            if (!features.TryGetValue(key, out var current) || current != value)
                return false;
        }

        return true;
    }

    public static string GetCurrentOsName() =>
        OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : "linux";

    public static string GetCurrentOsArch() =>
        RuntimeInformation.ProcessArchitecture switch {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var architecture => architecture.ToString().ToLowerInvariant()
        };

    public static string? GetNativeClassifier(IReadOnlyDictionary<string, string> natives) {
        var os = GetCurrentOsName();
        var archKey = RuntimeInformation.ProcessArchitecture switch {
            Architecture.Arm64 => $"{os}-arm64",
            Architecture.Arm => $"{os}-arm32",
            _ => os
        };

        if (natives.TryGetValue(archKey, out var classifier))
            return classifier;

        if (archKey != os && natives.TryGetValue(os, out var fallback))
            return fallback;

        return null;
    }
}
