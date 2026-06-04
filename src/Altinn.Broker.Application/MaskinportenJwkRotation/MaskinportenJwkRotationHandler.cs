using Altinn.Broker.Application.SendSlackNotification;
using Altinn.Broker.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.MaskinportenJwkRotation;

public class MaskinportenJwkRotationHandler(
    IMaskinportenJwkRotationService rotationService,
    SendSlackNotificationHandler slackNotificationHandler,
    TimeProvider timeProvider,
    ILogger<MaskinportenJwkRotationHandler> logger)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ProcessScheduled(CancellationToken cancellationToken)
    {
        var todayUtc = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.Date);

        if (!IsFirstWeekdayOfMonth(todayUtc))
        {
            logger.LogInformation(
                "Skipping scheduled Maskinporten JWK rotation on {Date} because it is not the first weekday of the month.",
                todayUtc);
            return;
        }

        await Process(cancellationToken);
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task Process(CancellationToken cancellationToken)
    {
        try
        {
            var result = await rotationService.RotateAsync(cancellationToken);
            var summary = string.Join(
                " ",
                result.Clients.Select(client =>
                    $"Client {client.ClientName} ({client.ClientId}) rotated to kid {client.NewKid}. Keys before: {client.PreviousKeyCount}, keys after: {client.CurrentKeyCount}. Secret updated: {client.KeyVaultSecretName}. Verification scope: {client.VerificationScope}."));
            await slackNotificationHandler.Process(
                "Maskinporten JWK rotation completed",
                summary,
                ":white_check_mark:");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Maskinporten JWK rotation failed.");
            await slackNotificationHandler.Process(
                "Maskinporten JWK rotation failed",
                ex.Message);
            throw;
        }
    }

    public static bool IsFirstWeekdayOfMonth(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        for (var day = 1; day < date.Day; day++)
        {
            var candidate = new DateOnly(date.Year, date.Month, day);
            if (candidate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                return false;
            }
        }

        return true;
    }
}
