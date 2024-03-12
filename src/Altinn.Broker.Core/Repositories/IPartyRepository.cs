

using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IPartyRepository
{
    Task<PartyEntity?> GetParty(string organizationId, CancellationToken cancellationToken);
    Task InitializeParty(string organizationId, string partyId);
}
