using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfigureResource;
using Altinn.Broker.Application.GetResource;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/resource")]
[Authorize(Policy = AuthorizationConstants.ServiceOwner)]
public class ResourceController : Controller
{
    /// <summary>
    /// Configures a resource with settings to be used within the broker service.
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/> 
    /// - altinn:serviceowner <br/>
    /// </remarks>
    /// <response code="200">Resource configured successfully</response>
    /// <response code="400"><ul>
    /// <li>Invalid grace period format. Should follow ISO8601 standard for duration. Example: 'PT2H' for 2 hours</li>
    /// <li>Grace period cannot exceed 24 hours</li>
    /// <li>Max file transfer size cannot be negative</li>
    /// <li>Max file transfer size cannot be zero</li>
    /// <li>Max file transfer size cannot be set higher than the 2GB in production unless the resource has been pre-approved for disabled virus scan. Contact us @ Slack</li>
    /// <li>Max file transfer size cannot be set higher than 100GB in production because it has not yet been tested for it. Contact us @ Slack if you need it</li>
    /// <li>Invalid file transfer time to live format. Should follow ISO8601 standard for duration. Example: 'P30D' for 30 days</li>
    /// <li>Time to live cannot exceed 365 days</li>
    /// </ul></response>
    /// <response code="401">You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry</response>
    /// <response code="403">The resource needs to be registered as an Altinn 3 resource and it has to be associated with a service owner</response>
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

    /// <summary>
    /// Gets information about a resource configuration in broker
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/> 
    /// - altinn:serviceowner <br/>
    /// </remarks>
    /// <response code="200">Detailed information about the resource</response>
    /// <response code="401">You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry</response>
    /// <response code="403">The resource needs to be registered as an Altinn 3 resource and it has to be associated with a service owner</response>
    [HttpGet]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Route("{resourceId}")]
    public async Task<ActionResult> GetResource(string resourceId, [FromServices] GetResourceHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Process(resourceId, HttpContext.User, cancellationToken);

        return result.Match(
            (resource) => Ok(new ResourceExt()
            {
                ExternalServiceCodeLegacy = resource.ExternalServiceCodeLegacy,
                ExternalServiceEditionCodeLegacy = resource.ExternalServiceEditionCodeLegacy,
                FileTransferTimeToLive = resource.FileTransferTimeToLive.HasValue ? resource.FileTransferTimeToLive.Value.ToString() : null,
                MaxFileTransferSize = resource.MaxFileTransferSize,
                PurgeFileTransferAfterAllRecipientsConfirmed = resource.PurgeFileTransferAfterAllRecipientsConfirmed,
                PurgeFileTransferGracePeriod = resource.PurgeFileTransferGracePeriod.HasValue ? resource.PurgeFileTransferGracePeriod.Value.ToString() : null,
                UseManifestFileShim = resource.UseManifestFileShim
            }),
            Problem
        );
    }
    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
