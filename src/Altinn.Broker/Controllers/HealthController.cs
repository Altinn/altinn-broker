
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly DatabaseConnectionProvider _databaseConnectionProvider;

        public HealthController(DatabaseConnectionProvider databaseConnectionProvider){
            _databaseConnectionProvider = databaseConnectionProvider;
        }

        [HttpGet]
        public ActionResult HealthCheck(){
            var connection = _databaseConnectionProvider.GetConnection();
            var command = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM broker.actor_shipment_status_description", connection);
            var count = (long)(command.ExecuteScalar() ?? 0);
            if (count == 0){
                return BadRequest("Unable to query database. Is DatabaseOptions__ConnectionString set and is the database migrated?");
            }
            return Ok("Environment properly configured rev4");
        }
    }
}
