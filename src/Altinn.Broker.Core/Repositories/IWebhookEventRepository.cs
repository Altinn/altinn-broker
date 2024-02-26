namespace Altinn.Broker.Core.Repositories
{
    public interface IWebhookEventRepository
    {
        Task AddWebhookEventAsync(string id);
        Task DeleteWebhookEventAsync(string id);
        Task DeleteOldWebhookEvents();
    }
}
