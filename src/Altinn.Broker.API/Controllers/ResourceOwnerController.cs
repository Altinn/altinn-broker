using System.Net;
using System.Xml;

using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Models.ResourceOwner;

using Hangfire;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/resourceowner")]
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
    public async Task<ActionResult> CreateNewResourceOwner([FromBody] ResourceOwnerInitializeExt resourceOwnerInitializeExt)
    {
        var existingResourceOwner = await _resourceOwnerRepository.GetResourceOwner(resourceOwnerInitializeExt.OrganizationId);
        if (existingResourceOwner is not null)
        {
            return Problem(detail: "Resource owner already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        var fileTimeToLive = XmlConvert.ToTimeSpan(resourceOwnerInitializeExt.DeletionTime); // ISO8601 Duration
        await _resourceOwnerRepository.InitializeResourceOwner(resourceOwnerInitializeExt.OrganizationId, resourceOwnerInitializeExt.Name, fileTimeToLive);
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(resourceOwnerInitializeExt.OrganizationId);
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

