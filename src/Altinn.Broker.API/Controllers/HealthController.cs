using Microsoft.AspNetCore.Mvc;

using Npgsql;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("health")]
[ApiExplorerSettings(IgnoreApi = true)]
public class HealthController(NpgsqlDataSource databaseConnectionProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> HealthCheckAsync()
    {
        try
        {
            using var command = databaseConnectionProvider.CreateCommand("SELECT COUNT(*) FROM broker.file_transfer_status_description");
            var count = (long)(await command.ExecuteScalarAsync() ?? 0);
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

    [HttpGet("throw")]
    public IActionResult ThrowTestException()
    {
        throw new Exception("Dette er en test-exception fra /health/throw-endepunktet for å teste logging og Slack-varsling.");
    }
}
