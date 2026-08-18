using System.IO.Compression;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;

namespace Iridium.Extensions;

public static class MinecraftEntryExtensions {
    /// <summary>
    /// Extracts native libraries (lwjgl/openal etc.) from the native jars into the
    /// natives directory so that they can be loaded via -Djava.library.path.
    /// </summary>
    public static Task ExtractNativesAsync(this MinecraftEntry entry,
        IReadOnlyList<string> nativeJars,
        string? nativesDirectory = null,
        IMinecraftLayoutFactory? factory = null,
        CancellationToken cancellationToken = default) {
        var directory = nativesDirectory
            ?? (factory ?? new DefaultMinecraftLayoutFactory()).Create(entry.Format).GetNativesDirectory(entry);

        if (nativeJars.Count == 0)
            return Task.CompletedTask;

        return Task.Run(() => {
            Directory.CreateDirectory(directory);
            foreach (var jar in nativeJars) {
                try {
                    using var archive = ZipFile.OpenRead(jar);
                    foreach (var zipEntry in archive.Entries) {
                        if (zipEntry.FullName.StartsWith("META-INF/", StringComparison.Ordinal))
                            continue;

                        if (zipEntry.Name.Length == 0 || !Path.HasExtension(zipEntry.FullName))
                            continue;

                        if (Path.GetExtension(zipEntry.Name) is ".sha1" or ".git" or ".pom" or ".txt" or ".md")
                            continue;

                        var target = Path.Combine(directory, zipEntry.Name);
                        if (File.Exists(target))
                            continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        zipEntry.ExtractToFile(target, overwrite: false);
                    }
                } catch {
                    // Corrupt native jar; skip.
                }
            }
        }, cancellationToken);
    }
}
