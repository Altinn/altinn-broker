using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Services.Enums;

namespace Altinn.Broker.Core.Services;

public interface IEventBus
{
    Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string subjectOrganizationId, EventSubjectType eventSubjectType, Guid eventIdempotencyKey, CancellationToken cancellationToken = default);
}
