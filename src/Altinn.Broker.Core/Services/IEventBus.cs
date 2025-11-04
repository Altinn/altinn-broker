using Altinn.Broker.Core.Services.Enums;

namespace Altinn.Broker.Core.Services;

public interface IEventBus
{
    Task Publish(
        AltinnEventType type,
        string resourceId,
        string fileTransferId,
        string? organizationId = null,
        Guid? guid = null,
        AltinnEventSubjectRole? subjectRole = null,
        CancellationToken cancellationToken = default);
}
