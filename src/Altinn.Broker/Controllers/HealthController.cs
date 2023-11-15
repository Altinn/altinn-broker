
using Altinn.Broker.Persistence;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly DatabaseConnectionProvider _databaseConnectionProvider;
        private readonly IFileStore _fileStore;

        public HealthController(DatabaseConnectionProvider databaseConnectionProvider, IFileStore fileStore)
        {
            _databaseConnectionProvider = databaseConnectionProvider;
            _fileStore = fileStore;
        }

        [HttpGet]
        public async Task<ActionResult> HealthCheckAsync()
        {
            try
            {
                var connection = await _databaseConnectionProvider.GetConnectionAsync();
                var command = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM broker.file_status_description", connection);
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

            var storageAccountOnline = await _fileStore.IsOnline(null);
            if (!storageAccountOnline)
            {
                Console.Error.WriteLine("Health: Invalid storage account in StorageOptions!");
                return BadRequest("Invalid storage account in StorageOptions");
            }
            return Ok("Environment properly configured");
        }
    }
}
