using System.Net;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models.ServiceOwner;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/serviceowner")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(Policy = AuthorizationConstants.ServiceOwner)]
public class ServiceOwnerController(IServiceOwnerRepository serviceOwnerRepository, IHostEnvironment hostEnvironment, IResourceManager resourceManager) : Controller
{
    [HttpPost]
    public async Task<ActionResult> InitializeServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt, CancellationToken cancellationToken)
    {
        var existingServiceOwner = await serviceOwnerRepository.GetServiceOwner(HttpContext.User.GetCallerOrganizationId());
        if (existingServiceOwner is not null)
        {
            return Problem(detail: "Service owner already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        await serviceOwnerRepository.InitializeServiceOwner(HttpContext.User.GetCallerOrganizationId(), serviceOwnerInitializeExt.Name);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(HttpContext.User.GetCallerOrganizationId());
        resourceManager.CreateStorageProviders(serviceOwner, cancellationToken);
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner(CancellationToken cancellationToken)
    {
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(HttpContext.User.GetCallerOrganizationId());
        if (serviceOwner is null)
        {
            return NotFound();
        }

        var deploymentStatuses = new Dictionary<StorageProviderEntity, DeploymentStatus>();
        foreach (var storageProvider in serviceOwner.StorageProviders)
        {
            var deploymentStatus = await resourceManager.GetDeploymentStatus(storageProvider, cancellationToken);
            deploymentStatuses.Add(storageProvider, deploymentStatus);
        }
        
        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            StorageProviders = deploymentStatuses.Zip(serviceOwner.StorageProviders, (status, provider) => new StorageProviderExt()
            {
                Type = (StorageProviderTypeExt)provider.Type,
                DeploymentEnvironment = hostEnvironment.EnvironmentName,
                DeploymentStatus = (DeploymentStatusExt)status.Value
            }).ToList()
        };
    }

}

