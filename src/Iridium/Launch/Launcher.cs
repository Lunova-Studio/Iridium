using System.Diagnostics;
using Iridium.Extensions;
using Iridium.Interfaces.Launch;
using Iridium.Models.Launch;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;

namespace Iridium.Launch;

public sealed class Launcher {
    private readonly IMinecraftArgumentParser? _resolver;

    public Launcher(IMinecraftArgumentParser? resolver = null) {
        _resolver = resolver;
    }

    public async Task<MinecraftProcess> LaunchAsync(MinecraftEntry entry, LaunchConfig config, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(config);
        if (config.JavaPath is null)
            throw new InvalidOperationException("JavaPath is required");

        var resolver = _resolver ?? MinecraftArgumentParserFactory.Create(entry);
        var arguments = resolver.Build(entry, config);
        var directories = LaunchDirectories.Resolve(
            MinecraftLayoutFactory.Create(entry), entry, config);

        if (arguments.Natives.Count > 0)
            await entry.ExtractNativesAsync(arguments.Natives, directories.NativesDirectory, cancellationToken);

        List<string> launchArgs = [.. arguments.JvmArguments, arguments.MainClass, .. arguments.GameArguments];
        var startInfo = new ProcessStartInfo(config.JavaPath.JavaPath) {
            WorkingDirectory = directories.GameDirectory,
            Arguments = string.Join(' ', launchArgs),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start Minecraft process: {startInfo.FileName}");

        return new MinecraftProcess(process, launchArgs);
    }
}
