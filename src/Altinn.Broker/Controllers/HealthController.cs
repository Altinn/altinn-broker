
using Altinn.Broker.Persistence;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly DatabaseConnectionProvider _databaseConnectionProvider;

        public HealthController(DatabaseConnectionProvider databaseConnectionProvider)
        {
            _databaseConnectionProvider = databaseConnectionProvider;
        }

        [HttpGet]
        public async Task<ActionResult> HealthCheckAsync()
        {
            try
            {
                var connection = await _databaseConnectionProvider.GetConnectionAsync();
                var command = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM broker.file_status", connection);
                var count = (long)(command.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    Console.Error.WriteLine("Health: Unable to query database. Is DatabaseOptions__ConnectionString set and is the database migrated");
                    return BadRequest("Unable to query database. Is DatabaseOptions__ConnectionString set and is the database migrated?");
                }
            }
            catch
            {
                Console.Error.WriteLine("Health: Exception thrown while trying to query database");
                return BadRequest("Exception thrown while trying to query database");
            }
            return Ok("Environment properly configured rev6");
        }
    }
}
