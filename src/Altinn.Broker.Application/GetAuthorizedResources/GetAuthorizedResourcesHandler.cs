using System.Security.Claims;

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
/// Lists the broker resources ("Dine formidlingstjenester") an authenticated end user has access
/// to on behalf of a party. Access is decided by a single multi-decision PDP request covering every
/// resource configured in broker.
/// </summary>
public class GetAuthorizedResourcesHandler(
    IAuthorizationService authorizationService,
    IResourceRepository resourceRepository,
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

        var configuredResourceIds = (await resourceRepository.GetResources(cancellationToken))
            .Where(resource => !string.IsNullOrWhiteSpace(resource.ServiceOwnerId))
            .Select(resource => resource.Id)
            .ToList();
        if (configuredResourceIds.Count == 0)
        {
            return new List<AuthorizedResourceOverview>();
        }

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

        var accessibleResources = authorizedResources
            .Where(authorized => authorized.CanSend || authorized.CanReceive)
            .ToList();
        logger.LogInformation(
            "End user has access to {authorizedResourceCount} of {configuredResourceCount} broker resources for the requested party",
            accessibleResources.Count,
            configuredResourceIds.Count);

        var overviews = await Task.WhenAll(accessibleResources.Select(async authorized =>
        {
            var metadata = await GetResourceMetadata(authorized.ResourceId, cancellationToken);
            return new AuthorizedResourceOverview
            {
                ResourceId = authorized.ResourceId,
                Name = metadata?.Title,
                ServiceOwnerName = metadata?.ServiceOwnerName,
                CanSend = authorized.CanSend,
                CanReceive = authorized.CanReceive
            };
        }));

        return overviews
            .OrderBy(overview => overview.Name ?? overview.ResourceId, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Reads the presentation metadata for a resource. Resource Registry failures degrade to a
    /// listing without name and owner rather than failing the whole request.
    /// </summary>
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
