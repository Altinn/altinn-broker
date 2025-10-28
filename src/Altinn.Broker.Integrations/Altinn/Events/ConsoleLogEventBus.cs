using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class ConsoleLogEventBus(ILogger<ConsoleLogEventBus> logger) : IEventBus
{
    public Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string organizationId, EventSubjectType eventSubjectType, Guid eventIdempotencyKey, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{CloudEventType} event raised on instance {fileTransferId} for party {eventSubjectType} eventSubjectTypewith organization number {organizationId}",
                              type.ToString(),
                              fileTransferId,
                              eventSubjectType,
                              organizationId);
        return Task.CompletedTask;
    }
}
