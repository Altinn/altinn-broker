using Hangfire.PostgreSql;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Altinn.Broker.Integrations.Hangfire;

public class HangfireDatabaseConnectionFactory(IServiceProvider serviceProvider) : IConnectionFactory
{
    public NpgsqlConnection GetOrCreateConnection() =>
        serviceProvider.GetRequiredService<NpgsqlDataSource>().CreateConnection();
}
