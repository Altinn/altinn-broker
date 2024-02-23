using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Application;
using OneOf;

namespace Altinn.Broker.Helpers;

public class WebhookEventHelper
{
    public static async Task<OneOf<Task, Error>> ProcessMalwareEvent(ScanResultData data, MalwareScanningResultHandler handler, IWebhookEventRepository webhookEventRepository)
    {
        try
        {

            // Create a new entry for that webhook id
            await webhookEventRepository.AddWebhookEventAsync(data.ETag);
            try
            {
                // Call you method
                return await handler.Process(data);
            }
            catch (Exception e)
            {
                // Delete the entry on error to make sure the next one isn't ignored
                await webhookEventRepository.DeleteWebhookEventAsync(data.ETag);
                return Task.CompletedTask;
            }
        }
        catch (Npgsql.PostgresException e)
        {
            // PostgreSQL code for unique violation
            if (e.SqlState == "23505")
            {
                // We are already processing or processed this webhook, return silently
                return Task.CompletedTask;
            }
            else throw e;

        }
    }
}

