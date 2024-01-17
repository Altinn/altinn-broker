using System.Net;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models.Service;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/resource")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ResourceController : Controller
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;

    public ResourceController(IResourceRepository serviceRepository, IResourceOwnerRepository resourceOwnerRepository)
    {
        _resourceRepository = serviceRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
    }

    [HttpPost]
    [Authorize(Policy = "ResourceOwner")]
    public async Task<ActionResult> RegisterResource([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, ResourceInitializeExt resourceInitializeExt)
    {
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(token.Consumer);
        if (resourceOwner is null)
        {
            return Problem(detail: "Resource owner not registered to use the broker API. Contact Altinn.", statusCode: (int)HttpStatusCode.Unauthorized);
        }

        var existingResource = await _resourceRepository.GetResource(resourceInitializeExt.ResourceId);
        if (existingResource is not null)
        {
            return Problem(detail: "Resource already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        await _resourceRepository.InitializeResource(resourceOwner.Id, resourceInitializeExt.OrganizationId, resourceInitializeExt.ResourceId);

        // Todo, add permitted system users to ad hoc resource rights registry

        return Ok();
    }

    [HttpGet]
    [Route("{resourceId}")]
    [Authorize(Policy = "ResourceOwner")]
    public async Task<ActionResult<ResourceOverviewExt>> GetResourceConfiguration(string resourceId)
    {
        var service = await _resourceRepository.GetResource(resourceId);
        if (service is null)
        {
            return NotFound();
        }

        return new ResourceOverviewExt()
        {
            ClientId = service.ClientId,
            Created = service.Created,
            OrganizationNumber = service.OrganizationNumber
        };
    }

    [HttpGet]
    [Authorize(Policy = "ResourceOwner")]
    public async Task<ActionResult<List<string>>> GetAllResources([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token)
    {
        var services = await _resourceRepository.SearchResources(token.Consumer);
        if (services is null)
        {
            return NotFound();
        }

        return services;
    }
}
