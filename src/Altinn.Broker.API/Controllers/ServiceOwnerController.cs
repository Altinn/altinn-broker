﻿using System.Net;
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
public class ServiceOwnerController(IServiceOwnerRepository serviceOwnerRepository, IResourceManager resourceManager) : Controller
{
    [HttpPost]
    public async Task<ActionResult> InitializeServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
    {
        var existingServiceOwner = await serviceOwnerRepository.GetServiceOwner(token.Consumer);
        if (existingServiceOwner is not null)
        {
            return Problem(detail: "Service owner already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        var fileTransferTimeToLive = XmlConvert.ToTimeSpan(serviceOwnerInitializeExt.DeletionTime);
        await serviceOwnerRepository.InitializeServiceOwner(token.Consumer, serviceOwnerInitializeExt.Name);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(token.Consumer);
        BackgroundJob.Enqueue(
            () => resourceManager.Deploy(serviceOwner!, cancellationToken)
        );

        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, CancellationToken cancellationToken)
    {
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(token.Consumer);
        if (serviceOwner is null)
        {
            return NotFound();
        }

        var deploymentStatus = await resourceManager.GetDeploymentStatus(serviceOwner, cancellationToken);

        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            DeploymentStatus = (DeploymentStatusExt)deploymentStatus
        };
    }

}

