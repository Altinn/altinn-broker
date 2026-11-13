using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GetEligibleParties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/eligible-party")]
public class EligiblePartyController(ILogger<EligiblePartyController> logger) : Controller
{
    [HttpGet]
    [Route("{organizationNumber}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GetPartyEligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Policy = AuthorizationConstants.EligibleParties)]
    public async Task<ActionResult> GetPartyEligibility(
        string organizationNumber,
        [FromServices] GetPartyEligibilityHandler handler,
        CancellationToken cancellationToken
    )
    {
        logger.LogDebug("Checking if party {organizationNumber} is eligible", organizationNumber);
        var result = await handler.Process(organizationNumber, HttpContext.User, cancellationToken);

        return result.Match(
            (eligibleParty) => Ok(eligibleParty),
            Problem
        );        
    }

    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GetEligiblePartiesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Policy = AuthorizationConstants.EligibleParties)]
    public async Task<ActionResult> GetEligibleParties(
        [FromServices] GetEligiblePartiesHandler handler,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Getting all eligible parties");
        var result = await handler.Process(HttpContext.User, cancellationToken);

        return result.Match(
            (eligibleParties) => Ok(eligibleParties),
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}

