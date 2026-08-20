using Iridium.Enums.Resources;
using Iridium.Models.Resources;

namespace Iridium.Interfaces.Resources;


public interface IResourceClient {

    ResourceSource Source { get; }


    ResourceApiOptions Options { get; }


    Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default);


    Task<IReadOnlyList<ResourceCategory>> GetCategoriesAsync(ResourceType type, CancellationToken cancellationToken = default);
}
