using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfigureResource;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/resource")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(Policy = AuthorizationConstants.ServiceOwner)]
public class ResourceController : Controller
{
    /// <summary>
    /// Configures a resource with settings to be used within the broker service.
    /// </summary>
    /// <remarks>
    /// Scopes: <br/> 
    /// - altinn:serviceowner <br/>
    /// </remarks>
    /// <returns></returns>
    [HttpPut]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Route("{resourceId}")]
    public async Task<ActionResult> ConfigureResource(string resourceId, [FromBody] ResourceExt resourceExt, [FromServices] ConfigureResourceHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Process(new ConfigureResourceRequest()
        {
            ResourceId = resourceId,
            MaxFileTransferSize = resourceExt.MaxFileTransferSize,
            FileTransferTimeToLive = resourceExt.FileTransferTimeToLive,
            PurgeFileTransferAfterAllRecipientsConfirmed = resourceExt.PurgeFileTransferAfterAllRecipientsConfirmed,
            PurgeFileTransferGracePeriod = resourceExt.PurgeFileTransferGracePeriod,
            UseManifestFileShim = resourceExt.UseManifestFileShim,
            ExternalServiceCodeLegacy = resourceExt.ExternalServiceCodeLegacy,
            ExternalServiceEditionCodeLegacy = resourceExt.ExternalServiceEditionCodeLegacy
        }, HttpContext.User, cancellationToken);

        return result.Match(
            (_) => Ok(null),
            Problem
        );
    }
    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
