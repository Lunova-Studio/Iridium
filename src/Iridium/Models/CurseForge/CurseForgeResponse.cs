using System.Text.Json;
using System.Text.Json.Serialization;

namespace Iridium.Models.CurseForge;


public sealed record CurseForgeResponse<T> {
    [JsonPropertyName("data")] public T? Data { get; init; }
}


public sealed record CurseForgePagedResponse<T> {
    [JsonPropertyName("data")] public T? Data { get; init; }
    [JsonPropertyName("pagination")] public CurseForgePagination? Pagination { get; init; }
}

public sealed record CurseForgePagination {
    [JsonPropertyName("index")] public int? Index { get; init; }
    [JsonPropertyName("pageSize")] public int? PageSize { get; init; }
    [JsonPropertyName("resultCount")] public int? ResultCount { get; init; }
    [JsonPropertyName("totalCount")] public int? TotalCount { get; init; }
}


public sealed record CurseForgeFeaturedResult {
    [JsonPropertyName("popular")] public List<CurseForgeProject> Popular { get; init; } = [];
    [JsonPropertyName("featured")] public List<CurseForgeProject> Featured { get; init; } = [];
}


public sealed record CurseForgeFingerprintResult {
    [JsonPropertyName("data")] public CurseForgeFingerprintData? Data { get; init; }
}

public sealed record CurseForgeFingerprintData {
    [JsonPropertyName("isMatch")] public bool IsMatch { get; init; }
    [JsonPropertyName("exactMatches")] public List<CurseForgeFingerprintMatch> ExactMatches { get; init; } = [];
    [JsonPropertyName("exactFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public List<uint> ExactFingerprints { get; init; } = [];
    [JsonPropertyName("partialMatches")] public List<CurseForgeFingerprintMatch> PartialMatches { get; init; } = [];
    [JsonPropertyName("partialMatchFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public List<uint> PartialMatchFingerprints { get; init; } = [];
    [JsonPropertyName("installedFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public List<uint> InstalledFingerprints { get; init; } = [];
    [JsonPropertyName("unmatchedFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public List<uint> UnmatchedFingerprints { get; init; } = [];
}

public sealed record CurseForgeFingerprintMatch {
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("file")] public CurseForgeFile? File { get; init; }
    [JsonPropertyName("latestFiles")] public List<CurseForgeFile> LatestFiles { get; init; } = [];
    [JsonPropertyName("project")] public CurseForgeProject? Project { get; init; }
    [JsonPropertyName("unmatchedFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public List<uint> UnmatchedFingerprints { get; init; } = [];
}


public sealed class TolerantUIntListConverter : JsonConverter<List<uint>> {
    public override List<uint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.StartObject) {
            if (reader.TokenType == JsonTokenType.StartObject)
                using (JsonDocument.ParseValue(ref reader)) { }
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
            return [];

        var list = new List<uint>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            if (reader.TokenType == JsonTokenType.Number)
                list.Add(reader.GetUInt32());
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<uint> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
