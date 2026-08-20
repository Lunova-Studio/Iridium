using Iridium.Interfaces.Resources;

namespace Iridium.Helpers.Resources;


public sealed class McimResourceMirror : IResourceMirror {
    private readonly string _baseUrl;

    public McimResourceMirror(string mirrorBaseUrl = "https://mod.mcimirror.top") {
        _baseUrl = mirrorBaseUrl.TrimEnd('/');
    }

    public string Name => "Mcim";

    public string? TryRewrite(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        string? prefix = uri.Host.ToLowerInvariant() switch {
            "api.modrinth.com" => $"{_baseUrl}/modrinth",
            "cdn.modrinth.com" => _baseUrl,
            "api.curseforge.com" => $"{_baseUrl}/curseforge",
            "edge.forgecdn.net" or "media.forgecdn.net" or "mediafiles.forgecdn.net" or
            "mediafilez.forgecdn.net" => _baseUrl,
            _ => null
        };

        return prefix is null ? null : $"{prefix}{uri.PathAndQuery}";
    }
}


public sealed class CustomResourceMirror : IResourceMirror {
    private readonly Func<string, string?> _rewrite;

    private CustomResourceMirror(string name, Func<string, string?> rewrite) {
        Name = name;
        _rewrite = rewrite;
    }

    public string Name { get; }


    public static CustomResourceMirror Create(string name, Func<string, string?> rewrite) =>
        new(name, rewrite);


    public static CustomResourceMirror CreateFromMap(string name,
        IReadOnlyDictionary<string, string> hostRewrites) {
        return new(name, url => {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;
            if (!hostRewrites.TryGetValue(uri.Host, out var baseUrl))
                return null;
            return $"{baseUrl.TrimEnd('/')}{uri.PathAndQuery}";
        });
    }

    public string? TryRewrite(string url) => _rewrite(url);
}
