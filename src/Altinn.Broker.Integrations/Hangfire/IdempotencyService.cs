using Altinn.Broker.Core.Repositories;

using Hangfire;

using Microsoft.Extensions.Logging;

public class IdempotencyService(
    IIdempotencyEventRepository idempotencyEventRepository,
    ILogger<IdempotencyService> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task DeleteOldIdempotencyEvents()
    {
        logger.LogInformation("Deleting old idempotency events");
        await idempotencyEventRepository.DeleteOldIdempotencyEvents();
    }
}
