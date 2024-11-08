
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

        return Ok("Environment properly configured");
    }
}
