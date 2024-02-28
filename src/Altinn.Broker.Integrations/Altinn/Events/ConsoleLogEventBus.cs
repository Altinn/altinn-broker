using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class ConsoleLogEventBus : IEventBus
{
    private readonly ILogger<ConsoleLogEventBus> _logger;

    public ConsoleLogEventBus(ILogger<ConsoleLogEventBus> logger)
    {
        _logger = logger;
    }

    public Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? organizationId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{CloudEventType} event raised on instance {fileTransferId} for party with organization number {organizationId}", type.ToString(), fileTransferId, organizationId);
        return Task.CompletedTask;
    }
}
