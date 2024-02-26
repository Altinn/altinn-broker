namespace Altinn.Broker.Core.Repositories
{
    public interface IWebhookEventRepository
    {
        Task AddWebhookEventAsync(string id, CancellationToken cancellationToken);
        Task DeleteWebhookEventAsync(string id, CancellationToken cancellationToken);
        Task DeleteOldWebhookEvents();
    }
}
