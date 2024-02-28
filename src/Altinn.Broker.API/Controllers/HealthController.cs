
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Persistence;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly DatabaseConnectionProvider _databaseConnectionProvider;
        private readonly AzureResourceManagerOptions _azureResourceManagerOptions;

        public HealthController(DatabaseConnectionProvider databaseConnectionProvider, IOptions<AzureResourceManagerOptions> azureResourceManagerOptions)
        {
            _databaseConnectionProvider = databaseConnectionProvider;
            _azureResourceManagerOptions = azureResourceManagerOptions.Value;
        }

        [HttpGet]
        public async Task<ActionResult> HealthCheckAsync()
        {
            try
            {
                using var connection = await _databaseConnectionProvider.GetConnectionAsync();
                var command = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM broker.file_transfer_status_description", connection);
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
            var credentials = new ClientSecretCredential(_azureResourceManagerOptions.TenantId, _azureResourceManagerOptions.ClientId, _azureResourceManagerOptions.ClientSecret);
            var armClient = new ArmClient(credentials);
            var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_azureResourceManagerOptions.SubscriptionId}"));
            var resourceGroupCollection = subscription.GetResourceGroups();
            var resourceGroupCount = resourceGroupCollection.Count();
            if (resourceGroupCount < 1)
            {
                return BadRequest($"Resource groups not found under subscription with id: {subscription.Id}. Are the service principal environment variables set?");
            }
            await resourceGroupCollection.GetAsync($"altinn-{_azureResourceManagerOptions.Environment}-broker-rg");

            return Ok("Environment properly configured");
        }
    }
}
