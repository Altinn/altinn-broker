using Altinn.Broker.Core.Services.Enums;

namespace Altinn.Broker.Core.Services;

public interface IEventBus
{
    Task Publish(AltinnEventType type, string resourceId, string fileId, CancellationToken cancellationToken);
}
