using Altinn.Broker.Core.Services;

using Hangfire;

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

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task UpdateIpRestrictions()
    {
        try
        {
            _logger.LogInformation("Started updating IP restrictions for container app");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            
            // Get the IPs from Event Grid and APIM
            var newIps = await _azureResourceManagerService.RetrieveCurrentIpRanges(cts.Token);
            _logger.LogInformation("Retrieved {Count} IP ranges", newIps.Count);
            
            if (newIps.Count < 1)
            {
                _logger.LogError("Failed to retrieve current IP ranges, canceling update of IP restrictions");
                return;
            }
            
            // Update the Container App with the new IPs
            await _azureResourceManagerService.UpdateContainerAppIpRestrictionsAsync(newIps, cts.Token);
            _logger.LogInformation("Successfully updated IP restrictions for container app");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("IP restrictions update operation timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while updating IP restrictions: {Message}", ex.Message);
            throw;
        }
    }
}
