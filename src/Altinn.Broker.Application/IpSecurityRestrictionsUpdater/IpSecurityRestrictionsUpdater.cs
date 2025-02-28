using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.IpSecurityRestrictionsUpdater;

public class IpSecurityRestrictionUpdater
{
    private readonly IResourceManager _azureResourceManagerService;
    
    private readonly ILogger<IpSecurityRestrictionUpdater> _logger;

    public IpSecurityRestrictionUpdater(IResourceManager azureResourceManagerService, ILogger<IpSecurityRestrictionUpdater> logger)
    {
        _azureResourceManagerService = azureResourceManagerService;
        _logger = logger;
    }

    public async Task UpdateIpRestrictions()
    {
        _logger.LogInformation("Updating IP restrictions for container app");
        var newIps = await _azureResourceManagerService.RetrieveCurrentIpRanges(CancellationToken.None);
        await _azureResourceManagerService.UpdateContainerAppIpRestrictionsAsync(newIps, CancellationToken.None);
    }
}
