using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Altinn.Broker.Integrations.Azure;
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection ConfigureOpenTelemetry(
        this IServiceCollection services,
        string applicationInsightsConnectionString)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            KeyValuePair.Create("service.name", (object)"altinn-broker"),
        };

        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder.AddAttributes(attributes))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http")
                    .AddNpgsqlInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Altinn.Broker.Integrations.Hangfire")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                        {
                            var path = httpContext.Request.Path.Value?.ToLowerInvariant();
                            return path != null &&
                                   !path.Contains("/health") &&
                                   !path.Contains("/migration");
                        };
                    })
                    .AddHttpClientInstrumentation();
            })
            .WithLogging(logging =>
            {
            });

        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            services.ConfigureOpenTelemetryMeterProvider(metrics =>
                metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = applicationInsightsConnectionString));

            services.ConfigureOpenTelemetryTracerProvider(tracing =>
                tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = applicationInsightsConnectionString));

            services.ConfigureOpenTelemetryLoggerProvider(logging =>
                logging.AddAzureMonitorLogExporter(o => o.ConnectionString = applicationInsightsConnectionString));
        }

        return services;
    }
}
