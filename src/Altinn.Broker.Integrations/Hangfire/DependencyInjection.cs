using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Altinn.Broker.Integrations.Slack;

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
            config.UseSerializerSettings(new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            config.UseFilter(
                new SlackExceptionHandler(
                    provider.GetRequiredService<SlackExceptionNotificationHandler>(),
                    provider.GetRequiredService<ILogger<SlackExceptionHandler>>())
                );
        }
        );
        services.AddHangfireServer();
    }
}
