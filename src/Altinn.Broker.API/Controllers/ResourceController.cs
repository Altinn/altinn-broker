using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfigureResourceCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Middlewares;
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
    [HttpPut]
    [Route("{resourceId}")]
    public async Task<ActionResult> ConfigureResource(string resourceId, [FromBody] ResourceExt resourceExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromServices] ConfigureResourceCommandHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Process(new ConfigureResourceCommandRequest()
        {
            Token = token,
            ResourceId = resourceId,
            MaxFileTransferSize = resourceExt.MaxFileTransferSize,
            FileTransferTimeToLive = resourceExt.FileTransferTimeToLive
        }, cancellationToken);

        return result.Match(
            (_) => Ok(null),
            Problem
        );
    }
    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
