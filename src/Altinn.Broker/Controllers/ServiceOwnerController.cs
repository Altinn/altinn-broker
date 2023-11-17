using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Helpers;
using Altinn.Broker.Models.ServiceOwner;
using Altinn.Broker.Persistence;

using Hangfire;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/serviceowner")]
public class ServiceOwnerController : ControllerBase
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly IResourceManager _resourceManager;

    public ServiceOwnerController(IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceManager resourceManager)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _brokerStorageService = brokerStorageService;
        _resourceManager = resourceManager;
    }

    [HttpPost]
    public async Task<ActionResult> CreateNewServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt)
    {
        var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
        if (string.IsNullOrWhiteSpace(caller))
        {
            return Unauthorized();
        }

        await _serviceOwnerRepository.InitializeServiceOwner(caller, serviceOwnerInitializeExt.Name);
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(caller);
        BackgroundJob.Enqueue(
            () => _resourceManager.Deploy(serviceOwner)
        );

        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner()
    {
        var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
        if (string.IsNullOrWhiteSpace(caller))
        {
            return Unauthorized();
        }
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(caller);
        if (serviceOwner is null)
        {
            return NotFound();
        }

        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);

        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            DeploymentStatus = (DeploymentStatusExt) deploymentStatus,
        };
    }
}

