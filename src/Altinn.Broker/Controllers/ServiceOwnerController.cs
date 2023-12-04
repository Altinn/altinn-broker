using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Helpers;
using Altinn.Broker.Models.ServiceOwner;

using Hangfire;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/serviceowner")]
public class ServiceOwnerController : Controller
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IResourceManager _resourceManager;

    public ServiceOwnerController(IServiceOwnerRepository serviceOwnerRepository, IResourceManager resourceManager)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _resourceManager = resourceManager;
    }

    [HttpPost]
    public async Task<ActionResult> CreateNewServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt)
    {
        var caller = MaskinportenHelper.GetCallerFromTestToken(HttpContext);
        if (string.IsNullOrWhiteSpace(caller))
        {
            return Unauthorized();
        }
        var existingServiceOwner = await _serviceOwnerRepository.GetServiceOwner(serviceOwnerInitializeExt.Id);
        if (existingServiceOwner is not null)
        {
            return Conflict("Service owner already exists");
        }

        await _serviceOwnerRepository.InitializeServiceOwner(serviceOwnerInitializeExt.Id, serviceOwnerInitializeExt.Name);
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(serviceOwnerInitializeExt.Id);
        BackgroundJob.Enqueue(
            () => _resourceManager.Deploy(serviceOwner!)
        );

        return Ok();
    }

    [HttpGet]
    [Route("{serviceOwnerId}")]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner(string serviceOwnerId)
    {
        var caller = MaskinportenHelper.GetCallerFromTestToken(HttpContext);
        if (string.IsNullOrWhiteSpace(caller))
        {
            return Unauthorized();
        }
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(serviceOwnerId);
        if (serviceOwner is null)
        {
            return NotFound();
        }

        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);

        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            DeploymentStatus = (DeploymentStatusExt)deploymentStatus,
        };
    }
}

