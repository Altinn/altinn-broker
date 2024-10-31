
using Altinn.Broker.Integrations.Azure;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("health")]
public class HealthController(NpgsqlDataSource databaseConnectionProvider, IOptions<AzureResourceManagerOptions> azureResourceManagerOptions) : ControllerBase
{
    private readonly AzureResourceManagerOptions _azureResourceManagerOptions = azureResourceManagerOptions.Value;

    [HttpGet]
    public async Task<ActionResult> HealthCheckAsync()
    {
        try
        {
            using var command = databaseConnectionProvider.CreateCommand("SELECT COUNT(*) FROM broker.file_transfer_status_description");
            var count = (long)(command.ExecuteScalar() ?? 0);
            if (count == 0)
            {
                Console.Error.WriteLine("Health: Unable to query database. Is DatabaseOptions__ConnectionString set and is the database migrated");
                return BadRequest("Unable to query database. Is DatabaseOptions__ConnectionString set and is the database migrated?");
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Health: Exception thrown while trying to query database: {exception}", e);
            return BadRequest("Exception thrown while trying to query database");
        }

        // Verify that resource manager has access to our subscription
        var credentials = new DefaultAzureCredential();
        var armClient = new ArmClient(credentials);
        var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_azureResourceManagerOptions.SubscriptionId}"));
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupCount = resourceGroupCollection.Count();
        if (resourceGroupCount < 1)
        {
            return BadRequest($"Resource groups not found under subscription with id: {subscription.Id}. Are the service principal environment variables set?");
        }
        await resourceGroupCollection.GetAsync(_azureResourceManagerOptions.ApplicationResourceGroupName);

        return Ok("Environment properly configured");
    }

    [HttpGet]
    [Route("ready")]
    public async Task<ActionResult> Ready()
    {
        return Ok();
    }
}
