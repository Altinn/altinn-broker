using Altinn.Broker.Persistence;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Altinn.Broker.Integrations.Hangfire;
public static class DependencyInjection
{
    public static void ConfigureHangfireDashboard(this WebApplication app, IConfiguration config)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseHangfireDashboard();
        }
        else
        {
            var hangfireAuthOptions = new HangfireAuthorizationOptions();
            config.GetSection(nameof(HangfireAuthorizationOptions)).Bind(hangfireAuthOptions);
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireMaintainerAuthorizationFilter(hangfireAuthOptions) }
            });
        }
    }

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
