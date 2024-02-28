using System.Net;
using System.Xml;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models.ServiceOwner;

using Hangfire;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/serviceowner")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(Policy = AuthorizationConstants.ServiceOwner)]
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
    public async Task<ActionResult> InitializeServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
    {
        var existingServiceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Consumer);
        if (existingServiceOwner is not null)
        {
            return Problem(detail: "Service owner already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        var fileTransferTimeToLive = XmlConvert.ToTimeSpan(serviceOwnerInitializeExt.DeletionTime);
        await _serviceOwnerRepository.InitializeServiceOwner(token.Consumer, serviceOwnerInitializeExt.Name, fileTransferTimeToLive);
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Consumer);
        BackgroundJob.Enqueue(
            () => _resourceManager.Deploy(serviceOwner!, cancellationToken)
        );

        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Consumer);
        if (serviceOwner is null)
        {
            return NotFound();
        }

        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner, cancellationToken);

        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            DeploymentStatus = (DeploymentStatusExt)deploymentStatus,
            FileTransferTimeToLive = serviceOwner.FileTransferTimeToLive
        };
    }
    [HttpPut]
    [Route("fileretention")]
    public async Task<ActionResult> UpdateFileRetention([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromBody] ServiceOwnerUpdateFileRetentionExt serviceOwnerUpdateFileRetentionExt, CancellationToken cancellationToken)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Consumer);
        if (serviceOwner is null)
        {
            return NotFound();
        }

        var fileTimeToLive = XmlConvert.ToTimeSpan(serviceOwnerUpdateFileRetentionExt.FileTransferTimeToLive);
        await _serviceOwnerRepository.UpdateFileRetention(token.Consumer, fileTimeToLive);

        return Ok();
    }

}

