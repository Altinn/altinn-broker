using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
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
    private readonly IResourceRepository _resourceRepository;
    private readonly Core.Repositories.IAuthorizationService _resourceRightsRepository;
    private readonly IResourceManager _resourceManager;

    public ResourceController(IResourceRepository resourceRepository, IResourceManager resourceManager, Core.Repositories.IAuthorizationService resourceRightsRepository)
    {
        _resourceRepository = resourceRepository;
        _resourceManager = resourceManager;
        _resourceRightsRepository = resourceRightsRepository;

    }

    [HttpPut]
    [Route("maxfiletransfersize")]
    public async Task<ActionResult> UpdateMaxFileTransferSize([FromBody] ResourceExt resourceExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(resourceExt.ResourceId, token.ClientId, [ResourceAccessLevel.Write], false, cancellationToken);
        if (!hasAccess)
        {
            return Unauthorized();
        }
        var resource = await _resourceRepository.GetResource(resourceExt.ResourceId, cancellationToken);

        if (resource is null)
        {
            return NotFound();
        }
        if (resourceExt.MaxFileTransferSize < 1)
        {
            return BadRequest("Max upload size cannot be negative");
        }
        if (resourceExt.MaxFileTransferSize == resource.MaxFileTransferSize)
        {
            return BadRequest("Max upload size is already set to the requested value");
        }
        long maxFileTransferSize = long.Parse(Environment.GetEnvironmentVariable("MAX_FILE_UPLOAD_SIZE"));

        if (resourceExt.MaxFileTransferSize > maxFileTransferSize)
        {
            return BadRequest("Max upload size cannot exceed the global maximum allowed size");
        }
        await _resourceRepository.UpdateMaxFileTransferSize(resourceExt.ResourceId, resourceExt.MaxFileTransferSize, cancellationToken);
        return Ok();
    }

}

