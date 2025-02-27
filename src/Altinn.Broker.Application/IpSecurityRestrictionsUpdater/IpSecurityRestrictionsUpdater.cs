using Altinn.Broker.Core.Services;

namespace Altinn.Broker.Application.IpSecurityRestrictionsUpdater;

public class IpSecurityRestrictionUpdater
{
    private readonly IResourceManager _azureResourceManagerService;

    public IpSecurityRestrictionUpdater(IResourceManager azureResourceManagerService)
    {
        _azureResourceManagerService = azureResourceManagerService;
    }

    public async Task UpdateIpRestrictions()
    {
        var newIps = await _azureResourceManagerService.RetrieveCurrentIpRanges(CancellationToken.None);
        await _azureResourceManagerService.UpdateContainerAppIpRestrictionsAsync(newIps);
    }
}
