using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;
namespace Altinn.Broker.Application.Middlewares;
public class EventBusMiddleware
{
    private readonly IEventBus _eventBus;
    public EventBusMiddleware(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
    [AutomaticRetry(Attempts = 0)]
    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? subjectOrganizationNumber = null)
    {
        await _eventBus.Publish(type, resourceId, fileTransferId, subjectOrganizationNumber);
    }
}
