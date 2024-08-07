using Altinn.Broker.Core.Options;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services, IConfiguration config)
    {
        var databaseOptions = new DatabaseOptions() { ConnectionString = "" };
        config.GetSection(nameof(DatabaseOptions)).Bind(databaseOptions);
        var serviceProvider = services.BuildServiceProvider();
        var connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(options => options.UseConnectionFactory(connectionFactory))
        );
        services.AddHangfireServer();
    }
}
