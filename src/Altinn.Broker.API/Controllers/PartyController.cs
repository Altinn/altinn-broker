using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GetAuthorizedParties;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/party")]
public class PartyController : Controller
{
    /// <summary>
    /// Get the parties the authenticated end user can act on behalf of.
    /// </summary>
    /// <remarks>
    /// Used by BrokerBox to populate the actor selector in the header. The list comes from
    /// Altinn Access Management and is personal to the logged in user.
    /// </remarks>
    /// <response code="200">The parties the end user can act on behalf of</response>
    /// <response code="401">You must be logged in as an end user</response>
    /// <response code="503">Altinn Access Management could not be reached</response>
    [HttpGet]
    [Route("authorized")]
    [Authorize(Policy = AuthorizationConstants.EndUser)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<AuthorizedPartyExt>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> GetAuthorizedParties(
        [FromServices] GetAuthorizedPartiesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(HttpContext.User, cancellationToken);

        return result.Match(
            (parties) => Ok(parties.Select(MapParty).ToList()),
            Problem
        );
    }

    private static AuthorizedPartyExt MapParty(AuthorizedParty party) => new()
    {
        PartyUuid = party.PartyUuid,
        Name = party.Name,
        OrganizationNumber = party.OrganizationNumber,
        PartyId = party.PartyId,
        Type = party.Type,
        IsDeleted = party.IsDeleted,
        OnlyHierarchyElementWithNoAccess = party.OnlyHierarchyElementWithNoAccess,
        Subunits = party.Subunits.Select(MapParty).ToList()
    };

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
