using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class WebhookEventRepository : IWebhookEventRepository
{
    private DatabaseConnectionProvider _connectionProvider;

    public WebhookEventRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }


    public async Task AddWebhookEventAsync(Guid WebhookEventId)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "INSERT INTO broker.webhook_event (webhook_event_id_pk, created)" +
                    "VALUES (@webhook_event_id_pk, @created) ");
        command.Parameters.AddWithValue("@webhook_event_id_pk", WebhookEventId);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }
    public async Task DeleteWebhookEventAsync(Guid WebhookEventId)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "DELETE FROM broker.webhook_event" +
                    "WHERE webhook_event_id_pk = @webhook_event_id_pk");
        command.Parameters.AddWithValue("@webhook_event_id_pk", WebhookEventId);

        await command.ExecuteNonQueryAsync();
    }
}