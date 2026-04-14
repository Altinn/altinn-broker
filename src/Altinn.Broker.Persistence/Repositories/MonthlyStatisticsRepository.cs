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
        CancellationToken cancellationToken)
    {
        var reportYear = fromInclusive.Year;
        var reportMonth = fromInclusive.Month;
        const string query = @"
            SELECT
                year,
                month,
                resource_id,
                sender,
                recipient,
                total_file_transfers,
                upload_count,
                total_transfer_download_attempts,
                transfers_with_download_confirmed
            FROM broker.monthly_statistics_rollup ms
            WHERE ms.service_owner_id = @serviceOwnerId
              AND ms.year = @year
              AND ms.month = @month
              AND (@resourceId IS NULL OR ms.resource_id = @resourceId)
            ORDER BY resource_id, sender, recipient;";

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            await using var command = dataSource.CreateCommand(query);
            command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
            command.Parameters.AddWithValue("@year", reportYear);
            command.Parameters.AddWithValue("@month", reportMonth);
            command.Parameters.Add(new NpgsqlParameter("@resourceId", NpgsqlDbType.Text)
            {
                Value = (object?)resourceId ?? DBNull.Value
            });

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
                    TotalFileTransfers = reader.GetInt32(reader.GetOrdinal("total_file_transfers")),
                    UploadCount = reader.GetInt32(reader.GetOrdinal("upload_count")),
                    TotalTransferDownloadAttempts = reader.GetInt32(reader.GetOrdinal("total_transfer_download_attempts")),
                    TransfersWithDownloadConfirmed = reader.GetInt32(reader.GetOrdinal("transfers_with_download_confirmed"))
                });
            }

            return monthlyStatistics;
        }, cancellationToken);
    }

    public async Task RebuildMonthlyStatisticsRollupForMonth(int year, int month, CancellationToken cancellationToken)
    {
        var fromInclusive = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusive = fromInclusive.AddMonths(1);

        const string query = @"
            DELETE FROM broker.monthly_statistics_rollup
            WHERE year = @year
            AND month = @month;

            WITH
            actor_activity AS (
                SELECT
                    afs.file_transfer_id_fk,
                    afs.actor_id_fk,
                    SUM(CASE WHEN afs.actor_file_transfer_status_description_id_fk = @downloadStartedStatus
                            THEN 1 ELSE 0 END)::int AS total_transfer_download_attempts_delta,
                    MAX(CASE WHEN afs.actor_file_transfer_status_description_id_fk = @downloadConfirmedStatus
                            THEN 1 ELSE 0 END)::int AS transfers_with_download_confirmed_delta
                FROM broker.actor_file_transfer_status afs
                WHERE afs.actor_file_transfer_status_date >= @fromInclusive
                  AND afs.actor_file_transfer_status_date < @toExclusive
                GROUP BY afs.file_transfer_id_fk, afs.actor_id_fk
            ),

            pair_context AS (
                SELECT
                    aa.file_transfer_id_fk,
                    aa.actor_id_fk,
                    ar.service_owner_id_fk AS service_owner_id,
                    f.resource_id,
                    f.created,
                    sender.actor_external_id AS sender,
                    recipient.actor_external_id AS recipient,
                    aa.total_transfer_download_attempts_delta,
                    aa.transfers_with_download_confirmed_delta
                FROM actor_activity aa
                JOIN broker.file_transfer f        ON f.file_transfer_id_pk  = aa.file_transfer_id_fk
                JOIN broker.altinn_resource ar     ON ar.resource_id_pk      = f.resource_id
                JOIN broker.actor sender           ON sender.actor_id_pk     = f.sender_actor_id_fk
                JOIN broker.actor recipient        ON recipient.actor_id_pk  = aa.actor_id_fk
            ),

            published_in_month AS (
                SELECT DISTINCT file_transfer_id_fk
                FROM broker.file_transfer_status
                WHERE file_transfer_status_description_id_fk = @publishedStatus
                AND file_transfer_status_date >= @fromInclusive
                AND file_transfer_status_date < @toExclusive
            ),

            month_summary AS (
                SELECT
                    pc.service_owner_id,
                    pc.resource_id,
                    pc.sender,
                    pc.recipient,
                    SUM(CASE WHEN pc.created >= @fromInclusive
                                AND pc.created < @toExclusive         THEN 1 ELSE 0 END)::int AS total_file_transfers,
                    SUM(CASE WHEN pim.file_transfer_id_fk IS NOT NULL   THEN 1 ELSE 0 END)::int AS upload_count,
                    SUM(COALESCE(pc.total_transfer_download_attempts_delta, 0))::int             AS total_transfer_download_attempts,
                    SUM(COALESCE(pc.transfers_with_download_confirmed_delta, 0))::int            AS transfers_with_download_confirmed
                FROM pair_context pc
                LEFT JOIN published_in_month pim
                    ON pim.file_transfer_id_fk = pc.file_transfer_id_fk
                GROUP BY
                    pc.service_owner_id,
                    pc.resource_id,
                    pc.sender,
                    pc.recipient
            )

            INSERT INTO broker.monthly_statistics_rollup (
                service_owner_id, year, month, resource_id, sender, recipient,
                total_file_transfers, upload_count,
                total_transfer_download_attempts, transfers_with_download_confirmed,
                refreshed_at
            )
            SELECT
                ms.service_owner_id, @year, @month, ms.resource_id, ms.sender, ms.recipient,
                ms.total_file_transfers, ms.upload_count,
                ms.total_transfer_download_attempts, ms.transfers_with_download_confirmed,
                NOW()
            FROM month_summary ms;";

        await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            await using var command = dataSource.CreateCommand(query);
            command.CommandTimeout = 600;
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@month", month);
            command.Parameters.AddWithValue("@fromInclusive", fromInclusive);
            command.Parameters.AddWithValue("@toExclusive", toExclusive);
            command.Parameters.AddWithValue("@publishedStatus", (int)FileTransferStatus.Published);
            command.Parameters.AddWithValue("@downloadStartedStatus", (int)ActorFileTransferStatus.DownloadStarted);
            command.Parameters.AddWithValue("@downloadConfirmedStatus", (int)ActorFileTransferStatus.DownloadConfirmed);

            return await command.ExecuteNonQueryAsync(ct);
        }, cancellationToken);
    }
}
