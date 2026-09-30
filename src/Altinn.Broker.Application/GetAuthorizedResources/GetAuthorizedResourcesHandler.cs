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
/// Lists the broker resources an end user has access to on behalf of a party.
/// </summary>
public class GetAuthorizedResourcesHandler(
    IAuthorizationService authorizationService,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IAltinnResourceRepository altinnResourceRepository,
    HybridCache hybridCache,
    ILogger<GetAuthorizedResourcesHandler> logger) : IHandler<GetAuthorizedResourcesRequest, List<AuthorizedResourceOverview>>
{
    private static readonly HybridCacheEntryOptions ResourceMetadataCacheOptions = new()
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

        var configuredResources = (await resourceRepository.GetResources(cancellationToken))
            .Where(resource => !string.IsNullOrWhiteSpace(resource.ServiceOwnerId))
            .ToList();
        if (configuredResources.Count == 0)
        {
            return new List<AuthorizedResourceOverview>();
        }

        var configuredResourceIds = configuredResources.Select(resource => resource.Id).ToList();
        var serviceOwnerByResourceId = configuredResources.ToDictionary(
            resource => resource.Id,
            resource => resource.ServiceOwnerId.WithoutPrefix(),
            StringComparer.Ordinal);

        List<AuthorizedResource> authorizedResources;
        try
        {
            authorizedResources = await authorizationService.GetAuthorizedResources(user, party, configuredResourceIds, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Could not get authorized resources from Altinn Authorization");
            return Errors.AuthorizationUnavailable;
        }

        var isServiceOwner = await serviceOwnerRepository.GetServiceOwner(party.WithPrefix()) is not null;
        var canConfigureForParty = isServiceOwner
            && await HasGatekeeperPublish(user, party, cancellationToken);

        var accessibleResources = authorizedResources
            .Where(authorized => authorized.CanSend
                || authorized.CanReceive
                || (canConfigureForParty
                    && serviceOwnerByResourceId.TryGetValue(authorized.ResourceId, out var owner)
                    && owner == party))
            .ToList();
        logger.LogInformation(
            "End user has access to {authorizedResourceCount} of {configuredResourceCount} broker resources for the requested party",
            accessibleResources.Count,
            configuredResourceIds.Count);

        var overviews = await Task.WhenAll(accessibleResources.Select(async authorized =>
        {
            var metadata = await GetResourceMetadata(authorized.ResourceId, cancellationToken);
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
                IsServiceOwner = isServiceOwner
            };
        }));

        return overviews
            .OrderBy(overview => overview.Name ?? overview.ResourceId, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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
