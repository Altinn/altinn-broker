using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Services;
public interface IAltinnAccessManagementService
{
    /// <summary>
    /// The parties the end user behind the current request is authorized to represent,
    /// limited to those with access to at least one of the given resources.
    /// </summary>
    Task<List<AuthorizedParty>> GetAuthorizedParties(IReadOnlyList<string> anyOfResourceIds, CancellationToken cancellationToken = default);
}
