using Iridium.Interfaces.Minecraft;

namespace Iridium.Providers.Minecraft;

public sealed class DefaultMinecraftProviderFactory : IMinecraftProviderFactory {
    public IMinecraftProvider Create(DirectoryInfo root) {
        ArgumentNullException.ThrowIfNull(root);

        if (Directory.Exists(Path.Combine(root.FullName, "instances")))
            return new PrismMinecraftProvider(root);

        return Directory.Exists(Path.Combine(root.FullName, "versions")) 
            ? new StandardMinecraftProvider(root) 
            : throw new ArgumentException($"Unrecognized Minecraft directory: {root.FullName}", nameof(root));
    }
}
