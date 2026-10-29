using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Test host that can inject Azure block-staging failures for TUS resume/reconcile scenarios.
/// </summary>
public sealed class TusStagingFailureTestFactory : CustomWebApplicationFactory
{
    public TusStagingFailureController StagingFailures { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITusStorageResolver>();
            services.AddScoped<TusStorageResolver>();
            services.AddSingleton(StagingFailures);
            services.AddScoped<ITusStorageResolver>(sp =>
                new FailingTusStorageResolverDecorator(
                    sp.GetRequiredService<TusStorageResolver>(),
                    sp.GetRequiredService<TusStagingFailureController>()));

            // HEAD should heal Accepted>durable wedges immediately in these tests.
            services.PostConfigure<TusOptions>(options =>
            {
                options.AcceptedOffsetReconcileGracePeriod = TimeSpan.Zero;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StagingFailures.Clear();
        }

        base.Dispose(disposing);
    }
}
