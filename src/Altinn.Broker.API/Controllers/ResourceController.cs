using System.Net;

using Altinn.Broker.API.Configuration;
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
[Authorize(Policy = AuthorizationConstants.ResourceOwner)]
public class ResourceController : Controller
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IResourceRightsRepository _resourceRightsRepository;

    public ResourceController(IResourceRepository resourceRepository, IResourceOwnerRepository resourceOwnerRepository, IResourceRightsRepository resourceRightsRepository)
    {
        _resourceRepository = resourceRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceRightsRepository = resourceRightsRepository;
    }

    [HttpPost]
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
        resourceInitializeExt.PermittedMaskinportenUsers.ForEach(async user =>
        {
            await _resourceRightsRepository.GiveUserAccess(user.ClientId, resourceInitializeExt.ResourceId, user.AccessLevel.ToString(), user.OrganizationNumber);
        });

        return Ok();
    }

    [HttpGet]
    [Route("{resourceId}")]
    public async Task<ActionResult<ResourceOverviewExt>> GetResourceConfiguration(string resourceId)
    {
        var resource = await _resourceRepository.GetResource(resourceId);
        if (resource is null)
        {
            return NotFound();
        }

        return new ResourceOverviewExt()
        {
            Id = resource.Id,
            Created = resource.Created,
            OrganizationNumber = resource.OrganizationNumber
        };
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetAllResources([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token)
    {
        var resources = await _resourceRepository.SearchResources(token.Consumer);
        if (resources is null)
        {
            return NotFound();
        }

        return resources;
    }
}
