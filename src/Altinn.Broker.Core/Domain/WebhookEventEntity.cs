namespace Altinn.Broker.Core.Domain;

public class WebhookEventEntity
{
    public string WebhookEventId { get; set; }
    public DateTimeOffset? Created { get; set; }
}
