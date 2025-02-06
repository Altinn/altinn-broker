using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfire(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionFactory, HangfireDatabaseConnectionFactory>();
        services.AddHangfire((provider, config) =>
        {
            config.UsePostgreSqlStorage(
                c => c.UseConnectionFactory(provider.GetRequiredService<IConnectionFactory>())
            );
            config.UseSerilogLogProvider();
            config.UseFilter(new HangfireAppRequestFilter(provider.GetRequiredService<TelemetryClient>()));
        }
        );
        services.AddHangfireServer();
    }
}
