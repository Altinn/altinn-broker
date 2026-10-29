using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetAuthorizedParties;

/// <summary>
/// Lists the parties the logged in end user can act on behalf of.
/// </summary>
public class GetAuthorizedPartiesHandler(
    IAltinnAccessManagementService accessManagementService,
    ILogger<GetAuthorizedPartiesHandler> logger) : IHandler<List<AuthorizedParty>>
{
    public async Task<OneOf<List<AuthorizedParty>, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        List<AuthorizedParty> parties;
        try
        {
            parties = await accessManagementService.GetAuthorizedParties(cancellationToken);
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
