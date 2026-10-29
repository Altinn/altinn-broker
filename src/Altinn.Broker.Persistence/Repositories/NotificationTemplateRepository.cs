using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class NotificationTemplateRepository(NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : INotificationTemplateRepository
{
    public async Task<NotificationTemplateEntity?> GetNotificationTemplate(NotificationTemplate notificationTemplate, string language, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT * FROM broker.notification_template WHERE notification_template = @notificationTemplate AND language = @language");
        command.Parameters.AddWithValue("@notificationTemplate", (int)notificationTemplate);
        command.Parameters.AddWithValue("@language", language);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return new NotificationTemplateEntity()
            {
                Id = reader.GetInt32(reader.GetOrdinal("notification_template_id_pk")),
                NotificationTemplate = (NotificationTemplate)reader.GetInt32(reader.GetOrdinal("notification_template")),
                Language = reader.GetString(reader.GetOrdinal("language")),
                EmailSubject = reader.IsDBNull(reader.GetOrdinal("email_subject")) ? null : reader.GetString(reader.GetOrdinal("email_subject")),
                EmailBody = reader.IsDBNull(reader.GetOrdinal("email_body")) ? null : reader.GetString(reader.GetOrdinal("email_body")),
                SmsBody = reader.IsDBNull(reader.GetOrdinal("sms_body")) ? null : reader.GetString(reader.GetOrdinal("sms_body")),
                ReminderEmailSubject = reader.IsDBNull(reader.GetOrdinal("reminder_email_subject")) ? null : reader.GetString(reader.GetOrdinal("reminder_email_subject")),
                ReminderEmailBody = reader.IsDBNull(reader.GetOrdinal("reminder_email_body")) ? null : reader.GetString(reader.GetOrdinal("reminder_email_body")),
                ReminderSmsBody = reader.IsDBNull(reader.GetOrdinal("reminder_sms_body")) ? null : reader.GetString(reader.GetOrdinal("reminder_sms_body"))
            };
        }, cancellationToken);
    }
}
