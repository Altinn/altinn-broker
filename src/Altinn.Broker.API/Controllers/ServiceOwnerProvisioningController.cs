using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ProvisionServiceOwner;
using Altinn.Broker.Models.ServiceOwner;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

/// <summary>
/// Lets a BrokerBox user provision the organization they represent.
/// </summary>
[ApiController]
[Route("broker/api/v1/serviceowner/provisioning")]
[Authorize(Policy = AuthorizationConstants.EndUser)]
public class ServiceOwnerProvisioningController : Controller
{
    /// <summary>
    /// Gets provisioning progress for an organization.
    /// </summary>
    /// <remarks>
    /// Requires an end-user session and publish on <c>digdir-broker-administrasjon</c> for <paramref name="party"/>.
    /// Poll this while <c>status</c> is <c>InProgress</c>. Storage deployment usually takes one to two minutes.
    /// </remarks>
    /// <response code="200">The current provisioning state. <c>NotRegistered</c> means provisioning has not been started.</response>
    /// <response code="400">The party is not a valid organization number</response>
    /// <response code="401">You must be logged in as an end user</response>
    /// <response code="403">You need publish on digdir-broker-administrasjon for the party</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ServiceOwnerProvisioningExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ServiceOwnerProvisioningExt>> Get(
        [FromQuery] string party,
        [FromServices] GetServiceOwnerProvisioningHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(party, HttpContext.User, cancellationToken);
        return result.Match(state => Ok(ToExt(state)), Problem);
    }

    /// <summary>
    /// Registers the organization in Broker and starts storage deployment.
    /// </summary>
    /// <remarks>
    /// Requires an end-user session and publish on <c>digdir-broker-administrasjon</c> for <paramref name="party"/>.
    /// The organization does not need to own a broker service yet. Calling this again while deployment is
    /// running returns the current progress and does not start a second deployment. A failed deployment can be started again.
    /// </remarks>
    /// <response code="200">Provisioning is ready or has been started</response>
    /// <response code="400">The party or name is invalid</response>
    /// <response code="401">You must be logged in as an end user</response>
    /// <response code="403">You need publish on digdir-broker-administrasjon for the party</response>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ServiceOwnerProvisioningExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServiceOwnerProvisioningExt>> Start(
        [FromQuery] string party,
        [FromBody] ServiceOwnerProvisioningRequestExt request,
        [FromServices] StartServiceOwnerProvisioningHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(new StartServiceOwnerProvisioningRequest
        {
            Party = party,
            Name = request.Name
        }, HttpContext.User, cancellationToken);
        return result.Match(state => Ok(ToExt(state)), Problem);
    }

    private static ServiceOwnerProvisioningExt ToExt(ServiceOwnerProvisioningState state) => new()
    {
        Status = state.Status,
        Phase = state.Phase,
        Percent = state.Percent
    };

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
