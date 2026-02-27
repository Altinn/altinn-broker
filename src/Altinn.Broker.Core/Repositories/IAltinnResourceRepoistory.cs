using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IAltinnResourceRepository
{
    Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the service owner name from Resource Registry for a given resource ID.
    /// This returns the name from HasCompetentAuthority.Name (e.g., "Digitaliseringsdirektoratet", "NAV", etc.)
    /// </summary>
    Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken = default);

    Task<List<string>?> GetAccessListOfResource(string resourceId, string party, CancellationToken cancellationToken = default);
}
