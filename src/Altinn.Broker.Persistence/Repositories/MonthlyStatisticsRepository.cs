using System.Text.Json;

using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Broker.Persistence.Repositories;

public class MonthlyStatisticsRepository(NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : IMonthlyStatisticsRepository
{
    public async Task<List<MonthlyResourceStatisticsData>> GetMonthlyResourceStatisticsData(
        string serviceOwnerId,
        DateTime fromInclusive,
        DateTime toExclusive,
        string? resourceId,
        IReadOnlyList<string>? groupByPropertyKeys,
        CancellationToken cancellationToken)
    {
        var reportYear = fromInclusive.Year;
        var reportMonth = fromInclusive.Month;
        const string query = @"
            WITH filtered_rollup AS (
                SELECT
                    year,
                    month,
                    resource_id,
                    sender,
                    recipient,
                    CASE
                        WHEN cardinality(@groupByPropertyKeys) = 0 THEN '{}'::jsonb
                        ELSE COALESCE(
                            (
                                SELECT jsonb_object_agg(properties.key, properties.value)
                                FROM jsonb_each_text(ms.groupable_property_values) AS properties
                                WHERE properties.key = ANY(@groupByPropertyKeys)
                            ),
                            '{}'::jsonb
                        )
                    END AS grouped_property_values,
                    total_file_transfers,
                    upload_count,
                    download_attempt_count,
                    unique_download_started_count,
                    download_confirmed_count
                FROM broker.monthly_statistics_monthly_rollup ms
                WHERE ms.service_owner_id = @serviceOwnerId
                  AND ms.year = @year
                  AND ms.month = @month
                  AND (@resourceId IS NULL OR ms.resource_id = @resourceId)
            )
            SELECT
                year,
                month,
                resource_id,
                sender,
                recipient,
                grouped_property_values::text AS grouped_property_values_json,
                SUM(total_file_transfers)::int AS total_file_transfers,
                SUM(upload_count)::int AS upload_count,
                SUM(download_attempt_count)::int AS download_attempt_count,
                SUM(unique_download_started_count)::int AS download_started_count_per_file_transfer,
                SUM(download_confirmed_count)::int AS download_confirmed_count
            FROM filtered_rollup
            GROUP BY year, month, resource_id, sender, recipient, grouped_property_values
            ORDER BY year, month, resource_id, sender, recipient, grouped_property_values::text;";

        await using var command = dataSource.CreateCommand(query);
        command.CommandTimeout = 600;
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        command.Parameters.AddWithValue("@year", reportYear);
        command.Parameters.AddWithValue("@month", reportMonth);
        command.Parameters.Add(new NpgsqlParameter("@resourceId", NpgsqlDbType.Text)
        {
            Value = (object?)resourceId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("@groupByPropertyKeys", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = (object?)(groupByPropertyKeys?.ToArray()) ?? Array.Empty<string>()
        });

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var monthlyStatistics = new List<MonthlyResourceStatisticsData>();
            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                monthlyStatistics.Add(new MonthlyResourceStatisticsData
                {
                    Year = reader.GetInt32(reader.GetOrdinal("year")),
                    Month = reader.GetInt32(reader.GetOrdinal("month")),
                    ResourceId = reader.GetString(reader.GetOrdinal("resource_id")),
                    Sender = reader.GetString(reader.GetOrdinal("sender")),
                    Recipient = reader.GetString(reader.GetOrdinal("recipient")),
                    GroupedPropertyValues = DeserializeGroupedPropertyValues(reader.GetString(reader.GetOrdinal("grouped_property_values_json"))),
                    TotalFileTransfers = reader.GetInt32(reader.GetOrdinal("total_file_transfers")),
                    UploadCount = reader.GetInt32(reader.GetOrdinal("upload_count")),
                    DownloadStartedCount = reader.GetInt32(reader.GetOrdinal("download_attempt_count")),
                    UniqueDownloadStartedCount = reader.GetInt32(reader.GetOrdinal("download_started_count_per_file_transfer")),
                    DownloadConfirmedCount = reader.GetInt32(reader.GetOrdinal("download_confirmed_count"))
                });
            }

            return monthlyStatistics;
        }, cancellationToken);
    }

    public async Task RefreshMonthlyStatisticsRollup(CancellationToken cancellationToken)
    {
        const string query = @"
            TRUNCATE TABLE broker.monthly_statistics_monthly_rollup;

            WITH recipient_pairs AS (
                SELECT DISTINCT
                    afs.file_transfer_id_fk,
                    afs.actor_id_fk
                FROM broker.actor_file_transfer_status afs
            ),
            published_transfers AS (
                SELECT DISTINCT
                    fts.file_transfer_id_fk
                FROM broker.file_transfer_status fts
                WHERE fts.file_transfer_status_description_id_fk = @publishedStatus
            ),
            transfer_properties AS (
                SELECT
                    f.file_transfer_id_pk,
                    COALESCE(properties.groupable_property_values, '{}'::jsonb) AS groupable_property_values
                FROM broker.file_transfer f
                LEFT JOIN (
                    SELECT
                        fp.file_transfer_id_fk,
                        jsonb_object_agg(fp.key, fp.value) AS groupable_property_values
                    FROM broker.file_transfer_property fp
                    GROUP BY fp.file_transfer_id_fk
                ) properties ON properties.file_transfer_id_fk = f.file_transfer_id_pk
            ),
            first_download_started AS (
                SELECT
                    afs.file_transfer_id_fk,
                    afs.actor_id_fk,
                    MIN(afs.actor_file_transfer_status_date) AS first_download_started_at
                FROM broker.actor_file_transfer_status afs
                WHERE afs.actor_file_transfer_status_description_id_fk = @downloadStartedStatus
                GROUP BY afs.file_transfer_id_fk, afs.actor_id_fk
            ),
            monthly_counts AS (
                SELECT
                    ar.service_owner_id_fk AS service_owner_id,
                    EXTRACT(YEAR FROM f.created)::int AS year,
                    EXTRACT(MONTH FROM f.created)::int AS month,
                    f.resource_id,
                    sender.actor_external_id AS sender,
                    recipient.actor_external_id AS recipient,
                    tp.groupable_property_values,
                    COUNT(*)::int AS total_file_transfers,
                    0::int AS upload_count,
                    0::int AS download_attempt_count,
                    0::int AS unique_download_started_count,
                    0::int AS download_confirmed_count
                FROM broker.file_transfer f
                INNER JOIN broker.altinn_resource ar ON ar.resource_id_pk = f.resource_id
                INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
                INNER JOIN recipient_pairs rp ON rp.file_transfer_id_fk = f.file_transfer_id_pk
                INNER JOIN broker.actor recipient ON recipient.actor_id_pk = rp.actor_id_fk
                INNER JOIN transfer_properties tp ON tp.file_transfer_id_pk = f.file_transfer_id_pk
                GROUP BY
                    ar.service_owner_id_fk,
                    EXTRACT(YEAR FROM f.created),
                    EXTRACT(MONTH FROM f.created),
                    f.resource_id,
                    sender.actor_external_id,
                    recipient.actor_external_id,
                    tp.groupable_property_values

                UNION ALL

                SELECT
                    ar.service_owner_id_fk AS service_owner_id,
                    EXTRACT(YEAR FROM f.created)::int AS year,
                    EXTRACT(MONTH FROM f.created)::int AS month,
                    f.resource_id,
                    sender.actor_external_id AS sender,
                    recipient.actor_external_id AS recipient,
                    tp.groupable_property_values,
                    0::int AS total_file_transfers,
                    COUNT(*)::int AS upload_count,
                    0::int AS download_attempt_count,
                    0::int AS unique_download_started_count,
                    0::int AS download_confirmed_count
                FROM broker.file_transfer f
                INNER JOIN published_transfers pt ON pt.file_transfer_id_fk = f.file_transfer_id_pk
                INNER JOIN broker.altinn_resource ar ON ar.resource_id_pk = f.resource_id
                INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
                INNER JOIN recipient_pairs rp ON rp.file_transfer_id_fk = f.file_transfer_id_pk
                INNER JOIN broker.actor recipient ON recipient.actor_id_pk = rp.actor_id_fk
                INNER JOIN transfer_properties tp ON tp.file_transfer_id_pk = f.file_transfer_id_pk
                GROUP BY
                    ar.service_owner_id_fk,
                    EXTRACT(YEAR FROM f.created),
                    EXTRACT(MONTH FROM f.created),
                    f.resource_id,
                    sender.actor_external_id,
                    recipient.actor_external_id,
                    tp.groupable_property_values

                UNION ALL

                SELECT
                    ar.service_owner_id_fk AS service_owner_id,
                    EXTRACT(YEAR FROM afs.actor_file_transfer_status_date)::int AS year,
                    EXTRACT(MONTH FROM afs.actor_file_transfer_status_date)::int AS month,
                    f.resource_id,
                    sender.actor_external_id AS sender,
                    recipient.actor_external_id AS recipient,
                    tp.groupable_property_values,
                    0::int AS total_file_transfers,
                    0::int AS upload_count,
                    COUNT(*)::int AS download_attempt_count,
                    0::int AS unique_download_started_count,
                    0::int AS download_confirmed_count
                FROM broker.actor_file_transfer_status afs
                INNER JOIN broker.file_transfer f ON f.file_transfer_id_pk = afs.file_transfer_id_fk
                INNER JOIN broker.altinn_resource ar ON ar.resource_id_pk = f.resource_id
                INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
                INNER JOIN broker.actor recipient ON recipient.actor_id_pk = afs.actor_id_fk
                INNER JOIN transfer_properties tp ON tp.file_transfer_id_pk = f.file_transfer_id_pk
                WHERE afs.actor_file_transfer_status_description_id_fk = @downloadStartedStatus
                GROUP BY
                    ar.service_owner_id_fk,
                    EXTRACT(YEAR FROM afs.actor_file_transfer_status_date),
                    EXTRACT(MONTH FROM afs.actor_file_transfer_status_date),
                    f.resource_id,
                    sender.actor_external_id,
                    recipient.actor_external_id,
                    tp.groupable_property_values

                UNION ALL

                SELECT
                    ar.service_owner_id_fk AS service_owner_id,
                    EXTRACT(YEAR FROM fds.first_download_started_at)::int AS year,
                    EXTRACT(MONTH FROM fds.first_download_started_at)::int AS month,
                    f.resource_id,
                    sender.actor_external_id AS sender,
                    recipient.actor_external_id AS recipient,
                    tp.groupable_property_values,
                    0::int AS total_file_transfers,
                    0::int AS upload_count,
                    0::int AS download_attempt_count,
                    COUNT(*)::int AS unique_download_started_count,
                    0::int AS download_confirmed_count
                FROM first_download_started fds
                INNER JOIN broker.file_transfer f ON f.file_transfer_id_pk = fds.file_transfer_id_fk
                INNER JOIN broker.altinn_resource ar ON ar.resource_id_pk = f.resource_id
                INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
                INNER JOIN broker.actor recipient ON recipient.actor_id_pk = fds.actor_id_fk
                INNER JOIN transfer_properties tp ON tp.file_transfer_id_pk = f.file_transfer_id_pk
                GROUP BY
                    ar.service_owner_id_fk,
                    EXTRACT(YEAR FROM fds.first_download_started_at),
                    EXTRACT(MONTH FROM fds.first_download_started_at),
                    f.resource_id,
                    sender.actor_external_id,
                    recipient.actor_external_id,
                    tp.groupable_property_values

                UNION ALL

                SELECT
                    ar.service_owner_id_fk AS service_owner_id,
                    EXTRACT(YEAR FROM afs.actor_file_transfer_status_date)::int AS year,
                    EXTRACT(MONTH FROM afs.actor_file_transfer_status_date)::int AS month,
                    f.resource_id,
                    sender.actor_external_id AS sender,
                    recipient.actor_external_id AS recipient,
                    tp.groupable_property_values,
                    0::int AS total_file_transfers,
                    0::int AS upload_count,
                    0::int AS download_attempt_count,
                    0::int AS unique_download_started_count,
                    COUNT(*)::int AS download_confirmed_count
                FROM broker.actor_file_transfer_status afs
                INNER JOIN broker.file_transfer f ON f.file_transfer_id_pk = afs.file_transfer_id_fk
                INNER JOIN broker.altinn_resource ar ON ar.resource_id_pk = f.resource_id
                INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
                INNER JOIN broker.actor recipient ON recipient.actor_id_pk = afs.actor_id_fk
                INNER JOIN transfer_properties tp ON tp.file_transfer_id_pk = f.file_transfer_id_pk
                WHERE afs.actor_file_transfer_status_description_id_fk = @downloadConfirmedStatus
                GROUP BY
                    ar.service_owner_id_fk,
                    EXTRACT(YEAR FROM afs.actor_file_transfer_status_date),
                    EXTRACT(MONTH FROM afs.actor_file_transfer_status_date),
                    f.resource_id,
                    sender.actor_external_id,
                    recipient.actor_external_id,
                    tp.groupable_property_values
            )
            INSERT INTO broker.monthly_statistics_monthly_rollup (
                service_owner_id,
                year,
                month,
                resource_id,
                sender,
                recipient,
                groupable_property_values,
                total_file_transfers,
                upload_count,
                download_attempt_count,
                unique_download_started_count,
                download_confirmed_count,
                refreshed_at
            )
            SELECT
                service_owner_id,
                year,
                month,
                resource_id,
                sender,
                recipient,
                groupable_property_values,
                SUM(total_file_transfers)::int AS total_file_transfers,
                SUM(upload_count)::int AS upload_count,
                SUM(download_attempt_count)::int AS download_attempt_count,
                SUM(unique_download_started_count)::int AS unique_download_started_count,
                SUM(download_confirmed_count)::int AS download_confirmed_count,
                NOW()
            FROM monthly_counts
            GROUP BY
                service_owner_id,
                year,
                month,
                resource_id,
                sender,
                recipient,
                groupable_property_values;";

        await using var command = dataSource.CreateCommand(query);
        command.CommandTimeout = 1800;
        command.Parameters.AddWithValue("@downloadStartedStatus", (int)ActorFileTransferStatus.DownloadStarted);
        command.Parameters.AddWithValue("@downloadConfirmedStatus", (int)ActorFileTransferStatus.DownloadConfirmed);
        command.Parameters.AddWithValue("@publishedStatus", (int)FileTransferStatus.Published);
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    private static Dictionary<string, string> DeserializeGroupedPropertyValues(string groupedPropertyValuesJson)
    {
        if (string.IsNullOrWhiteSpace(groupedPropertyValuesJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(groupedPropertyValuesJson) ?? [];
    }
}
