namespace Altinn.Broker.Core.Repositories
{
    public interface IWebhookEventRepository
    {
        Task AddWebhookEventAsync(Guid id);
        Task DeleteWebhookEventAsync(Guid id);
        Task DeleteOldWebhookEvents();
    }
}
