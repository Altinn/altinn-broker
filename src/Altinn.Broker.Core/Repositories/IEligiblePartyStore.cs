namespace Altinn.Broker.Core.Repositories;

public interface IEligiblePartyStore
{
    Task<bool> IsEligible(string organizationNumber, CancellationToken cancellationToken);
    Task<List<string>> GetAll(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, string>> GetKnownPartyMappings(IReadOnlyCollection<string> partyUuids, CancellationToken cancellationToken);
    Task ReplaceAll (IReadOnlyDictionary<string, string> partyUuidToOrganizationNumber, CancellationToken cancellationToken);
}