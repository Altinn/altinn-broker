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
[Route("broker/api/v1/service")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ServiceController : Controller
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;

    public ServiceController(IServiceRepository serviceRepository, IServiceOwnerRepository serviceOwnerRepository)
    {
        _serviceRepository = serviceRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
    }

    [HttpPost]
    [Authorize(Policy = "ServiceOwner")]
    public async Task<ActionResult> CreateNewService([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, ServiceInitializeExt serviceInitializeExt)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Consumer);
        if (serviceOwner is null)
        {
            return Problem(detail: "Service owner not registered to use the broker API. Contact Altinn.", statusCode: (int)HttpStatusCode.Unauthorized);
        }

        var existingService = await _serviceRepository.GetService(serviceInitializeExt.MaskinportenClientId);
        if (existingService is not null)
        {
            return Problem(detail: "Service already exists", statusCode: (int)HttpStatusCode.Conflict);
        }

        await _serviceRepository.InitializeService(serviceOwner.Id, serviceInitializeExt.OrganizationId, serviceInitializeExt.MaskinportenClientId);

        return Ok();
    }

    [HttpGet]
    [Route("{clientId}")]
    [Authorize(Policy = "ServiceOwner")]
    public async Task<ActionResult<ServiceOverviewExt>> GetService(string clientId)
    {
        var service = await _serviceRepository.GetService(clientId);
        if (service is null)
        {
            return NotFound();
        }

        return new ServiceOverviewExt()
        {
            ClientId = service.ClientId,
            Created = service.Created,
            OrganizationNumber = service.OrganizationNumber
        };
    }

    [HttpGet]
    [Authorize(Policy = "ServiceOwner")]
    public async Task<ActionResult<List<string>>> GetAllServices([ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token)
    {
        var services = await _serviceRepository.SearchServices(token.Consumer);
        if (services is null)
        {
            return NotFound();
        }

        return services;
    }
}
