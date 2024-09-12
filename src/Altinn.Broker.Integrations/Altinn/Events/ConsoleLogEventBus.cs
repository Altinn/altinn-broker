using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class ConsoleLogEventBus(ILogger<ConsoleLogEventBus> logger) : IEventBus
{
    public Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? organizationId = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{CloudEventType} event raised on instance {fileTransferId} for party with organization number {organizationId}", type.ToString(), fileTransferId, organizationId);
        return Task.CompletedTask;
    }
}
