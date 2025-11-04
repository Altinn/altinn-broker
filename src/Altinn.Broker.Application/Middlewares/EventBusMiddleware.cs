using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

namespace Altinn.Broker.Application.Middlewares;
public class EventBusMiddleware
{
    private readonly IEventBus _eventBus;
    public EventBusMiddleware(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? subjectOrganizationNumber = null, Guid? guid = null, AltinnEventSubjectRole? subjectRole = null)
    {
        await _eventBus.Publish(type, resourceId, fileTransferId, subjectOrganizationNumber, guid, subjectRole);
    }
}
