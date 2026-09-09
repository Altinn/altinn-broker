using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IAltinnResourceRepository
{
    Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the presentation metadata (title and service owner name) for a resource from Resource Registry.
    /// </summary>
    /// <returns>The metadata, or <see langword="null"/> when the resource is unknown to Resource Registry.</returns>
    Task<AltinnResourceMetadata?> GetResourceMetadata(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the service owner name from Resource Registry for a given resource ID.
    /// This returns the name from HasCompetentAuthority.Name (e.g., "Digitaliseringsdirektoratet", "NAV", etc.)
    /// </summary>
    Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets access-list memberships for a party and resource.
    /// </summary>
    /// <returns>
    /// The memberships, an empty list when the party has no membership, or <see langword="null"/>
    /// when the party is invalid or cannot be found.
    /// </returns>
    Task<List<string>?> GetAccessListOfResource(string resourceId, string party, CancellationToken cancellationToken = default);
}
