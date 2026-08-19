using System.Diagnostics;
using Iridium.Download;
using Iridium.Extensions;
using Iridium.Installation;
using Iridium.Interfaces.Launch;
using Iridium.Interfaces.Minecraft;
using Iridium.Models.Launch;
using Iridium.Models.Minecraft;
using Iridium.Parsers.Launch;

namespace Iridium.Launch;

public sealed class Launcher {
    private readonly IMinecraftLayoutFactory _factory;
    private readonly IMinecraftArgumentParser? _resolver;

    public Launcher(IMinecraftLayoutFactory? factory = null, IMinecraftArgumentParser? resolver = null) {
        _factory = factory ?? new DefaultMinecraftLayoutFactory();
        _resolver = resolver;
    }

    public async Task<MinecraftProcess> LaunchAsync(MinecraftEntry entry, LaunchConfig config, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(config);
        if (config.JavaPath is null)
            throw new InvalidOperationException("JavaPath is required");

        var layout = _factory.Create(entry.Format);
        var directories = LaunchDirectories.Resolve(layout, entry, config);

        // Deploy the un-hashed ("virtual") asset layout before the argument parser resolves
        // ${game_assets}/${assets_root}. For pre-1.6 indexes this also populates the game
        // directory's resources/ folder, which is where those versions load sounds from.
        await new AssetsReconstructor(layout)
            .ReconstructAsync(entry, directories.GameDirectory, cancellationToken)
            .ConfigureAwait(false);

        var resolver = _resolver ?? new StandardMinecraftArgumentParser(_factory);
        var arguments = resolver.Build(entry, config);

        if (arguments.Natives.Count > 0)
            await entry.ExtractNativesAsync(arguments.Natives, directories.NativesDirectory, cancellationToken: cancellationToken);

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
