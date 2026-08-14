using System.Text.Json;
using Iridium.Enums;
using Iridium.Models.Minecraft;

namespace Iridium.Parsers.Minecraft;

internal static class VersionJsonParser {
    public static MinecraftArguments? MapArguments(JsonElement root) {
        if (!root.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Object)
            return null;

        return new MinecraftArguments {
            Game = MapArgumentList(arguments, "game"),
            Jvm = MapArgumentList(arguments, "jvm")
        };
    }
    
    public static MinecraftVersionType MapType(JsonElement root) {
        if (!root.TryGetProperty("type", out var type) || type.GetString() is not { } value)
            return MinecraftVersionType.Release;

        return value switch {
            "snapshot" => MinecraftVersionType.Snapshot,
            "old_beta" => MinecraftVersionType.OldBeta,
            "old_alpha" => MinecraftVersionType.OldAlpha,
            _ => MinecraftVersionType.Release
        };
    }

    public static DateTime? MapReleaseTime(JsonElement root) {
        if (!root.TryGetProperty("releaseTime", out var releaseTime) || releaseTime.GetString() is not { } value)
            return null;

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    public static IReadOnlyList<MinecraftLibrary> MapLibraries(JsonElement libraries) {
            if (libraries.ValueKind != JsonValueKind.Array)
                return [];
    
            var result = new List<MinecraftLibrary>();
            var enumerable = libraries.EnumerateArray()
                .Where(library => library.ValueKind == JsonValueKind.Object);
            
            foreach (var library in enumerable) {
                if (!library.TryGetProperty("name", out var nameElement) || nameElement.GetString() is not { Length: > 0 } name)
                    continue;
    
                result.Add(new MinecraftLibrary {
                    Name = name,
                    Rules = MapRules(library),
                    Natives = MapNatives(library)
                });
            }
    
            return result;
        }
    
    public static IReadOnlyList<string> MapTweakers(JsonElement root) {
        if (!root.TryGetProperty("tweakers", out var tweakers) || tweakers.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<string>();
        foreach (var tweaker in tweakers.EnumerateArray())
            if (tweaker.GetString() is { Length: > 0 } value)
                result.Add(value);

        return result;
    }

    private static Dictionary<string, string>? MapNatives(JsonElement element) {
            if (!element.TryGetProperty("natives", out var natives) || natives.ValueKind != JsonValueKind.Object)
                return null;
    
            var result = new Dictionary<string, string>();
            foreach (var native in natives.EnumerateObject())
                result[native.Name] = native.Value.GetString() ?? string.Empty;
    
            return result;
    }
    
    private static List<CompatibilityRule>? MapRules(JsonElement element) {
            if (!element.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() == 0)
                return null;
    
            var result = new List<CompatibilityRule>();
            var allRules = rules.EnumerateArray().Where(rule => rule.ValueKind == JsonValueKind.Object);
            
            foreach (var rule in allRules) {
                var action = rule.TryGetProperty("action", out var actionElement) && actionElement.GetString() == "disallow"
                    ? CompatibilityRuleAction.Disallow
                    : CompatibilityRuleAction.Allow;
    
                string? osName = null;
                string? osVersion = null;
                string? osArch = null;
                if (rule.TryGetProperty("os", out var os) && os.ValueKind == JsonValueKind.Object) {
                    osName = os.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    osVersion = os.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : null;
                    osArch = os.TryGetProperty("arch", out var archElement) ? archElement.GetString() : null;
                }
    
                IReadOnlyDictionary<string, bool>? features = null;
                if (rule.TryGetProperty("features", out var featureElement) && featureElement.ValueKind == JsonValueKind.Object) {
                    var featuresDict = new Dictionary<string, bool>();
                    foreach (var feature in featureElement.EnumerateObject())
                        featuresDict[feature.Name] = feature.Value.GetBoolean();
                    
                    features = featuresDict;
                }
    
                result.Add(new CompatibilityRule {
                    Action = action,
                    OsName = osName,
                    OsVersion = osVersion,
                    OsArch = osArch,
                    Features = features
                });
            }
    
            return result.Count > 0 ? result : null;
        }
    
    private static List<MinecraftArgument> MapArgumentList(JsonElement arguments, string key) {
        if (!arguments.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<MinecraftArgument>();
        foreach (var item in list.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String) {
                if (item.GetString() is { Length: > 0 } value)
                    result.Add(new MinecraftArgument { Values = [value] });
            } else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var valueElement)) {
                var values = new List<string>();
                if (valueElement.ValueKind == JsonValueKind.Array) {
                    foreach (var element in valueElement.EnumerateArray())
                        if (element.GetString() is { Length: > 0 } value)
                            values.Add(value);
                } else if (valueElement.GetString() is { Length: > 0 } value)
                    values.Add(value);

                if (values.Count > 0)
                    result.Add(new MinecraftArgument { Values = values, Rules = MapRules(item) });
            }
        }

        return result;
    }
}