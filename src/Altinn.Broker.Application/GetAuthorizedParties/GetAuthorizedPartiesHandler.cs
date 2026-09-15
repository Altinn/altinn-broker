using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetAuthorizedParties;

/// <summary>
/// Lists the parties the logged in end user can act on behalf of.
/// </summary>
public class GetAuthorizedPartiesHandler(
    IAltinnAccessManagementService accessManagementService,
    IResourceRepository resourceRepository,
    ILogger<GetAuthorizedPartiesHandler> logger) : IHandler<List<AuthorizedParty>>
{
    public async Task<OneOf<List<AuthorizedParty>, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        // Only parties with access to a broker resource are of any use in BrokerBox.
        var configuredResourceIds = (await resourceRepository.GetResources(cancellationToken))
            .Where(resource => !string.IsNullOrWhiteSpace(resource.ServiceOwnerId))
            .Select(resource => resource.Id)
            .ToList();
        if (configuredResourceIds.Count == 0)
        {
            return new List<AuthorizedParty>();
        }

        List<AuthorizedParty> parties;
        try
        {
            parties = await accessManagementService.GetAuthorizedParties(configuredResourceIds, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Could not get authorized parties from Altinn Access Management");
            return Errors.AuthorizedPartiesUnavailable;
        }

        logger.LogInformation("End user can act on behalf of {partyCount} parties", parties.Count);
        return parties;
    }
}
