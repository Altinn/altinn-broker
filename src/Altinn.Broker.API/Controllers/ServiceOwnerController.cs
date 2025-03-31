using System.Net;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
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
    /// <summary>
    /// Initializes the service owner for the calling organization within the brokerservice.
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:serviceowner <br/>
    /// </remarks>
    /// <response code="200">Service owner initialized successfully</response>
    /// <response code="409">Service owner already exists</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> InitializeServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt, CancellationToken cancellationToken)
    {
        var existingServiceOwner = await serviceOwnerRepository.GetServiceOwner(HttpContext.User.GetCallerOrganizationId().WithPrefix());
        if (existingServiceOwner is not null)
        {
            return Problem(detail: "Service owner already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        await serviceOwnerRepository.InitializeServiceOwner(HttpContext.User.GetCallerOrganizationId().WithPrefix(), serviceOwnerInitializeExt.Name);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(HttpContext.User.GetCallerOrganizationId().WithPrefix());
        resourceManager.CreateStorageProviders(serviceOwner, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Gets the service owner for the calling organization within the brokerservice.
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:serviceowner <br/>
    /// </remarks>
    /// <response code="200">Service owner retrieved successfully</response>
    /// <response code="404">Service owner not found</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ServiceOwnerOverviewExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner(CancellationToken cancellationToken)
    {
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(HttpContext.User.GetCallerOrganizationId().WithPrefix());
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

