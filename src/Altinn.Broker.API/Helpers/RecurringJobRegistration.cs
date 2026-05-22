using Altinn.Broker.Application;
using Altinn.Broker.Application.CleanupUseCaseTests;
using Altinn.Broker.Application.IpSecurityRestrictionsUpdater;
using Altinn.Broker.Application.MaskinportenJwkRotation;
using Altinn.Broker.Application.MonthlyStatistics;
using Altinn.Broker.Core.Options;
using Hangfire;

namespace Altinn.Broker.API.Helpers;

public static class RecurringJobRegistration
{
    public const string MaskinportenJwkRotationJobId = "Rotate Maskinporten JWK and update Key Vault";
    // The first weekday of a month can only fall on day 1, 2 or 3. The handler validates the exact date.
    public const string MaskinportenJwkRotationCronExpression = "0 8 1-3 * *";

    public static void Register(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<IpSecurityRestrictionUpdater>(
            "Update IP restrictions to apimIp and current EventGrid IPs",
            handler => handler.UpdateIpRestrictions(),
            Cron.Daily());

        recurringJobManager.AddOrUpdate<StuckFileTransferHandler>(
            "Check for files stuck in UploadProcessing",
            handler => handler.CheckForStuckFileTransfers(CancellationToken.None),
            "*/30 * * * *");

        recurringJobManager.AddOrUpdate<RefreshMonthlyStatisticsRollupHandler>(
            "Refresh current month statistics rollup",
            handler => handler.RefreshRollup(CancellationToken.None),
            Cron.Weekly(DayOfWeek.Monday, 3));

        recurringJobManager.AddOrUpdate<RefreshMonthlyStatisticsRollupHandler>(
            "Finalize previous month statistics rollup",
            handler => handler.RefreshPreviousMonthRollup(CancellationToken.None),
            "0 4 2 * *");

        recurringJobManager.AddOrUpdate<CleanupUseCaseTestsHandler>(
            "Cleanup use case test data older than 1 day",
            handler => handler.Process(new CleanupUseCaseTestsRequest { MinAgeDays = 1 }, null, CancellationToken.None),
            Cron.Daily());

        var settings = configuration.GetSection(nameof(MaskinportenJwkRotationSettings)).Get<MaskinportenJwkRotationSettings>()
            ?? new MaskinportenJwkRotationSettings();

        if (!settings.Enabled)
        {
            recurringJobManager.RemoveIfExists(MaskinportenJwkRotationJobId);
            logger.LogInformation("Maskinporten JWK rotation job is disabled.");
            return;
        }

        recurringJobManager.AddOrUpdate<MaskinportenJwkRotationHandler>(
            MaskinportenJwkRotationJobId,
            handler => handler.ProcessScheduled(CancellationToken.None),
            MaskinportenJwkRotationCronExpression);

        logger.LogInformation("Maskinporten JWK rotation job registered with cron {CronExpression}.", MaskinportenJwkRotationCronExpression);
    }
}
