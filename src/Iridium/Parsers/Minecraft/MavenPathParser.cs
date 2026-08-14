namespace Iridium.Parsers.Minecraft;

internal static class MavenPathParser {
    public static string? Resolve(string librariesRoot, string name) {
        ReadOnlySpan<char> source = name.AsSpan();
        Span<Range> ranges = stackalloc Range[4];

        var count = source.Split(ranges, ':');
        if (count is not (3 or 4))
            return null;

        var classifier = count == 4 ? source[ranges[3]] : ReadOnlySpan<char>.Empty;
        var artifact = source[ranges[1]].ToString();
        var version = source[ranges[2]].ToString();
        var fileName = classifier.IsEmpty
            ? $"{artifact}-{version}.jar"
            : $"{artifact}-{version}-{classifier.ToString()}.jar";

        return Path.Combine(
            librariesRoot, 
            source[ranges[0]].ToString().Replace('.', Path.DirectorySeparatorChar),
            artifact,
            version,
            fileName);
    }
}