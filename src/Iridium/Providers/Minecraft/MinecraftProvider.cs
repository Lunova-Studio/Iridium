using Iridium.Interfaces.Minecraft;
using Iridium.Models.Minecraft;

namespace Iridium.Providers.Minecraft;

public sealed class MinecraftProvider : IMinecraftProvider {
    private readonly IMinecraftProvider _inner;

    public MinecraftProvider(DirectoryInfo root, IMinecraftProviderFactory? factory = null) {
        ArgumentNullException.ThrowIfNull(root);

        var providerFactory = factory ?? new DefaultMinecraftProviderFactory();
        _inner = providerFactory.Create(root);
    }

    public Task<MinecraftEntry?> GetMinecraftAsync(string id, CancellationToken cancellationToken = default)
        => _inner.GetMinecraftAsync(id, cancellationToken);
    
    public Task<IReadOnlyList<MinecraftEntry>> GetMinecraftsAsync(CancellationToken cancellationToken = default)
        => _inner.GetMinecraftsAsync(cancellationToken);
}