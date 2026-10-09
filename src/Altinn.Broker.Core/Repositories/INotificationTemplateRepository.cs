using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;

namespace Altinn.Broker.Core.Repositories;

public interface INotificationTemplateRepository
{
    Task<NotificationTemplateEntity?> GetNotificationTemplate(NotificationTemplate notificationTemplate, string language, CancellationToken cancellationToken);
}
