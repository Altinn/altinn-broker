using System.Security.Claims;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetAuthorizedResources;

/// <summary>
/// Lists the broker resources an end user has access to on behalf of a party:
/// access-list members with send or receive rights, plus every broker resource
/// owned by the party when it is a Broker service owner.
/// Includes BrokerService resources from Resource Registry that are not yet
/// ConfigureResource'd locally.
/// </summary>
public class GetAuthorizedResourcesHandler(
    IAuthorizationService authorizationService,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IAltinnResourceRepository altinnResourceRepository,
    BrokerResourceProvisioner resourceProvisioner,
    HybridCache hybridCache,
    ILogger<GetAuthorizedResourcesHandler> logger) : IHandler<GetAuthorizedResourcesRequest, List<AuthorizedResourceOverview>>
{
    private static readonly HybridCacheEntryOptions ResourceMetadataCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10)
    };

    private static readonly HybridCacheEntryOptions BrokerServiceSearchCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10)
    };

    public async Task<OneOf<List<AuthorizedResourceOverview>, Error>> Process(GetAuthorizedResourcesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var party = request.Party.WithoutPrefix();
        if (!party.IsOrganizationNumber())
        {
            return Errors.InvalidParty;
        }

        var brokerServices = await SearchBrokerServices(cancellationToken);
        // Persist BrokerServices with default config so detail/config endpoints work after listing.
        await resourceProvisioner.EnsureFromSearchHitsAsync(brokerServices, cancellationToken);

        var configuredResources = (await resourceRepository.GetResources(cancellationToken))
            .Where(resource => !string.IsNullOrWhiteSpace(resource.ServiceOwnerId))
            .ToList();

        var metadataFromSearch = brokerServices.ToDictionary(
            hit => hit.Id,
            hit => new AltinnResourceMetadata
            {
                Title = hit.Title,
                ServiceOwnerName = hit.ServiceOwnerName
            },
            StringComparer.Ordinal);

        // Local configured resources win for ownership; RR fills in BrokerServices not yet in Broker
        // (e.g. when the service owner is not configured in this environment yet).
        var serviceOwnerByResourceId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in configuredResources)
        {
            serviceOwnerByResourceId[resource.Id] = resource.ServiceOwnerId.WithoutPrefix();
        }
        foreach (var hit in brokerServices)
        {
            serviceOwnerByResourceId.TryAdd(hit.Id, hit.OrganizationNumber);
        }

        var candidateResourceIds = serviceOwnerByResourceId.Keys.ToList();
        if (candidateResourceIds.Count == 0)
        {
            return new List<AuthorizedResourceOverview>();
        }

        List<AuthorizedResource> authorizedResources;
        try
        {
            authorizedResources = await authorizationService.GetAuthorizedResources(user, party, candidateResourceIds, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Could not get authorized resources from Altinn Authorization");
            return Errors.AuthorizationUnavailable;
        }

        var isServiceOwner = await serviceOwnerRepository.GetServiceOwner(party.WithPrefix()) is not null;
        var canConfigureForParty = isServiceOwner
            && await HasGatekeeperPublish(user, party, cancellationToken);

        // Include when the party owns the resource as a Broker service owner, or when they can
        // send/receive and are on the resource access list.
        var accessibility = await Task.WhenAll(authorizedResources.Select(async authorized =>
        {
            var ownedByParty = serviceOwnerByResourceId.TryGetValue(authorized.ResourceId, out var owner)
                && owner == party;
            if (isServiceOwner && ownedByParty)
            {
                return (authorized, include: true);
            }

            if (!authorized.CanSend && !authorized.CanReceive)
            {
                return (authorized, include: false);
            }

            return (authorized, include: await IsOnAccessList(authorized.ResourceId, party, cancellationToken));
        }));

        var accessibleResources = accessibility
            .Where(entry => entry.include)
            .Select(entry => entry.authorized)
            .ToList();
        logger.LogInformation(
            "End user has access to {authorizedResourceCount} of {candidateResourceCount} broker resources for the requested party ({configuredCount} configured locally, {brokerServiceCount} BrokerService from registry)",
            accessibleResources.Count,
            candidateResourceIds.Count,
            configuredResources.Count,
            brokerServices.Count);

        var overviews = await Task.WhenAll(accessibleResources.Select(async authorized =>
        {
            var metadata = metadataFromSearch.TryGetValue(authorized.ResourceId, out var fromSearch)
                ? fromSearch
                : await GetResourceMetadata(authorized.ResourceId, cancellationToken);
            var ownedByParty = serviceOwnerByResourceId.TryGetValue(authorized.ResourceId, out var owner)
                && owner == party;
            return new AuthorizedResourceOverview
            {
                ResourceId = authorized.ResourceId,
                Name = metadata?.Title,
                ServiceOwnerName = metadata?.ServiceOwnerName,
                CanSend = authorized.CanSend,
                CanReceive = authorized.CanReceive,
                CanPublish = canConfigureForParty && ownedByParty,
                IsServiceOwner = isServiceOwner,
                IsOwned = ownedByParty
            };
        }));

        return overviews
            .OrderBy(overview => overview.Name ?? overview.ResourceId, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<AltinnResourceSearchHit>> SearchBrokerServices(CancellationToken cancellationToken)
    {
        try
        {
            return await hybridCache.GetOrCreateAsync(
                $"rr:search:{AltinnResourceTypes.BrokerService}",
                AltinnResourceTypes.BrokerService,
                (resourceType, token) => new ValueTask<IReadOnlyList<AltinnResourceSearchHit>>(
                    altinnResourceRepository.SearchResourcesByType(resourceType, token)),
                BrokerServiceSearchCacheOptions,
                cancellationToken: cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not search BrokerService resources in Altinn Resource Registry; continuing with locally configured resources only");
            return [];
        }
    }

    /// <remarks>
    /// A missing or empty membership means the party is not on the resource access list.
    /// Registry failures for one resource exclude that resource rather than failing the whole list.
    /// </remarks>
    private async Task<bool> IsOnAccessList(string resourceId, string party, CancellationToken cancellationToken)
    {
        try
        {
            var membership = await altinnResourceRepository.GetAccessListOfResource(resourceId, party, cancellationToken);
            return membership is { Count: > 0 };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(
                e,
                "Could not evaluate access-list membership for party {party} on resource {resourceId}",
                party.SanitizeForLogs(),
                resourceId.SanitizeForLogs());
            return false;
        }
    }

    private async Task<bool> HasGatekeeperPublish(
        ClaimsPrincipal? user,
        string party,
        CancellationToken cancellationToken)
    {
        try
        {
            return await authorizationService.CheckAccessAsPublisher(
                user,
                ApplicationConstants.BrokerBoxConfigureGatekeeperResourceId,
                party,
                cancellationToken);
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "Could not evaluate publish access on configuration gatekeeper for party {party}", party.SanitizeForLogs());
            return false;
        }
    }

    private async Task<AltinnResourceMetadata?> GetResourceMetadata(string resourceId, CancellationToken cancellationToken)
    {
        try
        {
            return await hybridCache.GetOrCreateAsync(
                $"resource-metadata:{resourceId}",
                resourceId,
                (id, token) => new ValueTask<AltinnResourceMetadata?>(altinnResourceRepository.GetResourceMetadata(id, token)),
                ResourceMetadataCacheOptions,
                cancellationToken: cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not get metadata for resource {resourceId} from Altinn Resource Registry", resourceId.SanitizeForLogs());
            return null;
        }
    }
}
