using Altinn.Broker.Persistence;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(
                c => c.UseConnectionFactory(serviceProvider.GetRequiredService<DatabaseConnectionProvider>())
            )
        );
        services.AddHangfireServer();
    }
}
