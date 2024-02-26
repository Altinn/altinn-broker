namespace Altinn.Broker.Core.Repositories
{
    public interface IWebhookEventRepository
    {
        Task AddWebhookEventAsync(string id, CancellationToken ct);
        Task DeleteWebhookEventAsync(string id, CancellationToken ct);
        Task DeleteOldWebhookEvents();
    }
}
