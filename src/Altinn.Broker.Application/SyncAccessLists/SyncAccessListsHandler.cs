using System.Collections.Concurrent;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.SyncAccessLists;

public class SyncAccessListsHandler(
    IAltinnResourceRepository altinnResourceRepository,
    IEligiblePartyStore eligiblePartyStore,
    IResourceRepository resourceRepository,
    IAltinnRegisterService registerService,
    ILogger<SyncAccessListsHandler> logger
)
{
    private const int MaxParallellRegisterLookups = 10;

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task Process(CancellationToken cancellationToken)
    {
        var partyUuids = await GetAllAccessListPartyUuids(cancellationToken);

        var known = await eligiblePartyStore.GetKnownPartyMappings(partyUuids, cancellationToken);

        var misses = partyUuids.Where(u => !known.ContainsKey(u)).ToList();
        var (resolved, failedLookups) = await ResolveInRegister(misses, cancellationToken);

        if (failedLookups > 0)
        {
            throw new InvalidOperationException(
                $"Eligible party sync aborted: {failedLookups} of {misses.Count} register lookups failed. Previous eligible parties are kept.");
        }

        var mappings = new Dictionary<string, string>(known);
        foreach (var (uuid, organizationNumber) in resolved)
        {
            mappings[uuid] = organizationNumber;
        }

        if (partyUuids.Count == 0)
        {
            logger.LogWarning("Access lists have no members. Clearing eligible parties");
        }
        else if (mappings.Count == 0)
        {
            logger.LogWarning("None of the {memberCount} access list members have an organization number. Clearing eligible parties", partyUuids.Count);
        }
        
        await eligiblePartyStore.ReplaceAll(mappings, cancellationToken);
        logger.LogInformation("Synced eligible parties: {partyCount} parties, {lookupCount} looked up in register, {unresolvedCount} not resolved", mappings.Count, misses.Count, misses.Count - resolved.Count);
    }

    private async Task<List<string>> GetAllAccessListPartyUuids(CancellationToken cancellationToken)
    {
        var partyUuids = new HashSet<string>();
        var resources = await resourceRepository.GetResources(cancellationToken);
        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accessList = await altinnResourceRepository.GetAccessListMembersOfResource(resource.Id, cancellationToken);
            if (accessList is null)
            {
                logger.LogWarning("Resource {resourceId} was not found in Resource Registry, skipping", resource.Id.SanitizeForLogs());
                continue;
            }
            partyUuids.UnionWith(accessList.PartyUuids);
        }
        return partyUuids.ToList();
    }

    private async Task<(IReadOnlyDictionary<string, string> Resolved, int FailedLookups)> ResolveInRegister(List<string> partyUuids, CancellationToken cancellationToken)
    {
        var resolved = new ConcurrentDictionary<string, string>();
        var failed = 0;
        await Parallel.ForEachAsync(
            partyUuids,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallellRegisterLookups, CancellationToken = cancellationToken },
            async (partyUuid, ct) =>
            {
                try
                {
                    var party = await registerService.LookupPartyByUuid(partyUuid, ct);
                    if (party is null || string.IsNullOrWhiteSpace(party.OrgNumber))
                    {
                        logger.LogWarning("Failed to resolve partyUuid {partyUuid} in register", partyUuid.SanitizeForLogs());
                        return;
                    }
                    resolved [partyUuid] = party.OrgNumber;
                }
                catch( Exception e) when (e is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failed);
                    logger.LogError(e, "Failed to resolve partyUuid {partyUuid} in register", partyUuid.SanitizeForLogs());
                }
            }
        );
        return (resolved, failed);
    }
}