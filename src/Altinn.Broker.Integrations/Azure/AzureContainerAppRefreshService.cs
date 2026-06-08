using Altinn.Broker.Core.Services;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Azure;

public class AzureContainerAppRefreshService(ILogger<AzureContainerAppRefreshService> logger) : IContainerAppRefreshService
{
    private const string RefreshEnvironmentVariableName = "MASKINPORTEN_JWK_ROTATION_REFRESHED_AT";
    private readonly ArmClient _armClient = new(new DefaultAzureCredential());

    public async Task RefreshAsync(string containerAppResourceId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(containerAppResourceId))
        {
            return;
        }

        var containerApp = await _armClient.GetContainerAppResource(new ResourceIdentifier(containerAppResourceId)).GetAsync(cancellationToken);
        var data = containerApp.Value.Data;
        var appContainer = data.Template.Containers.FirstOrDefault()
            ?? throw new InvalidOperationException($"Container app {containerAppResourceId} has no app container to refresh.");

        var refreshValue = DateTimeOffset.UtcNow.ToString("O");
        var refreshEnvironmentVariable = appContainer.Env.FirstOrDefault(env => env.Name == RefreshEnvironmentVariableName);
        if (refreshEnvironmentVariable is null)
        {
            appContainer.Env.Add(new ContainerAppEnvironmentVariable
            {
                Name = RefreshEnvironmentVariableName,
                Value = refreshValue
            });
        }
        else
        {
            refreshEnvironmentVariable.Value = refreshValue;
            refreshEnvironmentVariable.SecretRef = null;
        }

        logger.LogInformation(
            "Refreshing container app {ContainerAppResourceId} after Maskinporten JWK rotation. Reason: {Reason}",
            containerAppResourceId,
            reason);

        await containerApp.Value.UpdateAsync(WaitUntil.Started, data, cancellationToken);
    }
}
