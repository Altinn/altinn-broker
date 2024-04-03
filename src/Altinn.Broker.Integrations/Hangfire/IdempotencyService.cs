using Altinn.Broker.Core.Repositories;

using Hangfire;

using Microsoft.Extensions.Logging;

public class IdempotencyService
{
    private readonly IIdempotencyEventRepository _idempotencyEventRepository;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IIdempotencyEventRepository idempotencyEventRepository,
        ILogger<IdempotencyService> logger)
    {
        _idempotencyEventRepository = idempotencyEventRepository;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task DeleteOldIdempotencyEvents()
    {
        _logger.LogInformation("Deleting old idempotency events");
        await _idempotencyEventRepository.DeleteOldIdempotencyEvents();
    }
}
