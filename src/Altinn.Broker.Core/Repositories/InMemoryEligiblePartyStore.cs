using System.Collections.Frozen;

namespace Altinn.Broker.Core.Repositories;

public class InMemoryEligiblePartyStore : IEligiblePartyStore
{
    private sealed record Snapshot(FrozenSet<string> Organizations, FrozenDictionary<string, string> Mappings);

    private volatile Snapshot _snapshot = new(FrozenSet<string>.Empty, FrozenDictionary<string, string>.Empty);

    public Task<bool> IsEligible(string organizationNumber, CancellationToken cancellationToken)
        => Task.FromResult(_snapshot.Organizations.Contains(organizationNumber));

    public Task<List<string>> GetAll(CancellationToken cancellationToken)
        => Task.FromResult(_snapshot.Organizations.Select(o => o.ToString()).ToList());

    public Task<IReadOnlyDictionary<string, string>> GetKnownPartyMappings(IReadOnlyCollection<string> partyUuids, CancellationToken cancellationToken)
    {
        var mappings = _snapshot.Mappings;
        IReadOnlyDictionary<string, string> known = partyUuids
            .Where(mappings.ContainsKey)
            .ToDictionary(u => u, u => mappings[u]);
        return Task.FromResult(known);
    }

    public Task ReplaceAll(IReadOnlyDictionary<string, string> partyUuidToOrganizationNumber, CancellationToken cancellationToken)
    {
        var organizations = partyUuidToOrganizationNumber.Values.Where(o => !string.IsNullOrEmpty(o)).ToFrozenSet();
        if (organizations.Count == 0)
        {
            return Task.CompletedTask;
        }
        _snapshot = new Snapshot(organizations, partyUuidToOrganizationNumber.ToFrozenDictionary());
        return Task.CompletedTask;
    }
}
