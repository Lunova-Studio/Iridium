namespace Iridium.Parsers.Minecraft;

internal static class MavenPathParser {
    public static string? Resolve(string librariesRoot, string name) {
        if (GetRelativePath(name) is not { } relative)
            return null;

        return Path.Combine(librariesRoot, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Maven-relative path with forward slashes,
    /// e.g. net/minecraft/launchwrapper/1.12/launchwrapper-1.12.jar.
    /// Handles classifiers and the @extension suffix.
    /// </summary>
    public static string? GetRelativePath(string name) {
        var atIndex = name.IndexOf('@');
        var extension = atIndex >= 0 ? name[(atIndex + 1)..] : "jar";
        if (atIndex >= 0)
            name = name[..atIndex];

        ReadOnlySpan<char> source = name.AsSpan();
        Span<Range> ranges = stackalloc Range[4];

        var count = source.Split(ranges, ':');
        if (count is not (3 or 4))
            return null;

        var classifier = count == 4 ? source[ranges[3]] : ReadOnlySpan<char>.Empty;
        var group = source[ranges[0]].ToString();
        var artifact = source[ranges[1]].ToString();
        var version = source[ranges[2]].ToString();
        var fileName = classifier.IsEmpty
            ? $"{artifact}-{version}.{extension}"
            : $"{artifact}-{version}-{classifier.ToString()}.{extension}";

        return $"{group.Replace('.', '/')}/{artifact}/{version}/{fileName}";
    }
}