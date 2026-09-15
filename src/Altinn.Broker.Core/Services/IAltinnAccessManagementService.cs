using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Services;
public interface IAltinnAccessManagementService
{
    /// <summary>
    /// The parties the end user behind the current request is authorized to represent.
    /// </summary>
    Task<List<AuthorizedParty>> GetAuthorizedParties(CancellationToken cancellationToken = default);
}
