using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class ConsoleLogEventBus(ILogger<ConsoleLogEventBus> logger) : IEventBus
{
    public Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? organizationId = null, Guid? guid = null, AltinnEventSubjectRole? subjectRole = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "{CloudEventType} event raised on instance {fileTransferId} for party with organization number {organizationId} and role {role}",
            type.ToString(),
            fileTransferId,
            organizationId,
            subjectRole?.ToString() ?? "Unknown");
        return Task.CompletedTask;
    }
}
