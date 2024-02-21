using System.Net;
using System.Xml;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models.ResourceOwner;

using Hangfire;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/resourceowner")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(Policy = AuthorizationConstants.ResourceOwner)]
public class ResourceOwnerController : Controller
{
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IResourceManager _resourceManager;

    public ResourceOwnerController(IResourceOwnerRepository resourceOwnerRepository, IResourceManager resourceManager)
    {
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceManager = resourceManager;
    }

    [HttpPost]
    public async Task<ActionResult> InitializeResourceOwner([FromBody] ResourceOwnerInitializeExt resourceOwnerInitializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token)
    {
        var existingResourceOwner = await _resourceOwnerRepository.GetResourceOwner(token.Consumer);
        if (existingResourceOwner is not null)
        {
            return Problem(detail: "Resource owner already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        var fileTimeToLive = XmlConvert.ToTimeSpan(resourceOwnerInitializeExt.DeletionTime);
        await _resourceOwnerRepository.InitializeResourceOwner(token.Consumer, resourceOwnerInitializeExt.Name, fileTimeToLive);
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(token.Consumer);
        BackgroundJob.Enqueue(
            () => _resourceManager.Deploy(resourceOwner!)
        );

        return Ok();
    }

    [HttpGet]
    [Route("{resourceOwnerId}")]
    public async Task<ActionResult<ResourceOwnerOverviewExt>> GetResourceOwner(string resourceOwnerId)
    {
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(resourceOwnerId);
        if (resourceOwner is null)
        {
            return NotFound();
        }

        var deploymentStatus = await _resourceManager.GetDeploymentStatus(resourceOwner);

        return new ResourceOwnerOverviewExt()
        {
            Name = resourceOwner.Name,
            DeploymentStatus = (DeploymentStatusExt)deploymentStatus,
            FileTimeToLive = resourceOwner.FileTimeToLive
        };
    }
}

