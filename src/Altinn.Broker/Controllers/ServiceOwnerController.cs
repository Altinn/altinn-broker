using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Helpers;
using Altinn.Broker.Models.ServiceOwner;
using Altinn.Broker.Persistence;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/serviceowner")]
public class ServiceOwnerController : ControllerBase
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileStore _fileStore;

    public ServiceOwnerController(IServiceOwnerRepository serviceOwnerRepository, IFileStore fileStore)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileStore = fileStore;
    }

    [HttpPost]
    public async Task<ActionResult> CreateNewServiceOwner([FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt)
    {
        var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
        if (string.IsNullOrWhiteSpace(caller))
        {
            return Unauthorized();
        }

        await _serviceOwnerRepository.InitializeServiceOwner(caller, serviceOwnerInitializeExt.Name, serviceOwnerInitializeExt.StorageAccountConnectionString);

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

        var isOnline = await _fileStore.IsOnline(serviceOwner.StorageAccountConnectionString);

        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            StorageAccountOnline = isOnline
        };
    }
}
