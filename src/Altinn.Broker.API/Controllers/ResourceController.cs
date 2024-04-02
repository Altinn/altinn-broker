using System.Xml;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Models;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;
using Altinn.Broker.Models.ServiceOwner;

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

    public ResourceController(IResourceRepository resourceRepository, Core.Repositories.IAuthorizationService resourceRightsRepository)
    {
        _resourceRepository = resourceRepository;
        _resourceRightsRepository = resourceRightsRepository;

    }

    [HttpPut]
    [Route("maxfiletransfersize")]
    public async Task<ActionResult> UpdateMaxFileTransferSize([FromBody] ResourceMaxFileTransferSizeRequest resourceExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
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
        if (resourceExt.MaxFileTransferSize < 0)
        {
            return BadRequest("Max upload size cannot be negative");
        }
        if (resourceExt.MaxFileTransferSize == 0)
        {
            return BadRequest("Max upload size cannot be zero");
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

    [HttpPut]
    [Route("filetransfertimetolive")]
    public async Task<ActionResult> UpdateFileTransferTimeToLive([FromBody] ResourceFileTransferTimeToLiveRequest resourceExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
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
        TimeSpan fileTransferTimeToLive;
        try 
        {
            fileTransferTimeToLive = XmlConvert.ToTimeSpan(resourceExt.FileTransferTimeToLive);
        }
        catch (FormatException)
        {
            return BadRequest("Invalid file transfer time to live format. Should follow ISO8601 standard for duration. Example: 'P30D' for 30 days.");
        }
        if (fileTransferTimeToLive > TimeSpan.FromDays(365))
        {
            return BadRequest("Time to live cannot exceed 365 days");
        }

        await _resourceRepository.UpdateFileRetention(resourceExt.ResourceId, resourceExt.FileTransferTimeToLive, cancellationToken);
        return Ok();
    }
}
