using System.Security.Claims;
using System.Text;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.MonthlyStatistics;

public class GetMonthlyStatisticsCsvHandler(
    IMonthlyStatisticsRepository monthlyStatisticsRepository,
    IResourceRepository resourceRepository,
    ILogger<GetMonthlyStatisticsCsvHandler> logger) : IHandler<GetMonthlyStatisticsReportRequest, GetMonthlyStatisticsCsvResponse>
{
    public async Task<OneOf<GetMonthlyStatisticsCsvResponse, Error>> Process(
        GetMonthlyStatisticsReportRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        if (request.Year < 1 || request.Year > 9999 || request.Month < 1 || request.Month > 12 || (request.Year == 9999 && request.Month == 12))
        {
            return StatisticsErrors.InvalidMonthFormat;
        }

        if (!request.IncludeEndUser && !request.IncludeVendor)
        {
            return StatisticsErrors.EndUserAndVendorBothExcluded;
        }

        var fromMonthStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusive = fromMonthStart.AddMonths(1);

        var callerOrganizationId = user?.GetCallerOrganizationId();
        if (string.IsNullOrWhiteSpace(callerOrganizationId))
        {
            return Errors.NoAccessToResource;
        }

        var resourceId = string.IsNullOrWhiteSpace(request.ResourceId) ? null : request.ResourceId.Trim();

        logger.LogInformation(
            "Generating monthly statistics CSV for service owner {ServiceOwnerId} for {Year}-{Month} (includeEndUser={IncludeEndUser}, includeVendor={IncludeVendor})",
            callerOrganizationId.SanitizeForLogs(),
            request.Year,
            request.Month,
            request.IncludeEndUser,
            request.IncludeVendor);

        if (resourceId is not null)
        {
            var resource = await resourceRepository.GetResource(resourceId, cancellationToken);
            if (resource is null || string.IsNullOrWhiteSpace(resource.OrganizationNumber))
            {
                return Errors.ResourceHasNotBeenConfigured;
            }

            if (resource.OrganizationNumber.WithoutPrefix() != callerOrganizationId.WithoutPrefix())
            {
                return Errors.NoAccessToResource;
            }
        }

        var rows = await monthlyStatisticsRepository.GetMonthlyResourceStatisticsData(
            serviceOwnerId: callerOrganizationId.WithPrefix(),
            fromInclusive: fromMonthStart,
            toExclusive: toExclusive,
            resourceId: resourceId,
            cancellationToken: cancellationToken);

        var projected = Project(rows, request.IncludeEndUser, request.IncludeVendor);

        var response = new GetMonthlyStatisticsCsvResponse
        {
            Content = Encoding.UTF8.GetBytes(BuildCsv(projected)),
            FileName = BuildFileName(resourceId, fromMonthStart),
            RowCount = projected.Count
        };

        return response;
    }

    private static List<MonthlyResourceStatisticsData> Project(
        IReadOnlyList<MonthlyResourceStatisticsData> rows,
        bool includeEndUser,
        bool includeVendor)
    {
        return rows
            .GroupBy(row => new
            {
                row.Year,
                row.Month,
                row.ResourceId,
                Sender = includeEndUser ? row.Sender : string.Empty,
                Recipient = includeEndUser ? row.Recipient : string.Empty,
                SenderSystemVendor = includeVendor ? row.SenderSystemVendor : string.Empty,
                RecipientSystemVendor = includeVendor ? row.RecipientSystemVendor : string.Empty
            })
            .Select(group => new MonthlyResourceStatisticsData
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                ResourceId = group.Key.ResourceId,
                Sender = group.Key.Sender,
                Recipient = group.Key.Recipient,
                SenderSystemVendor = group.Key.SenderSystemVendor,
                RecipientSystemVendor = group.Key.RecipientSystemVendor,
                TotalFileTransfers = group.Sum(row => row.TotalFileTransfers),
                UploadCount = group.Sum(row => row.UploadCount),
                TotalTransferDownloadAttempts = group.Sum(row => row.TotalTransferDownloadAttempts),
                TransfersWithDownloadConfirmed = group.Sum(row => row.TransfersWithDownloadConfirmed)
            })
            .OrderBy(row => row.ResourceId, StringComparer.Ordinal)
            .ThenBy(row => row.Sender, StringComparer.Ordinal)
            .ThenBy(row => row.SenderSystemVendor, StringComparer.Ordinal)
            .ThenBy(row => row.Recipient, StringComparer.Ordinal)
            .ThenBy(row => row.RecipientSystemVendor, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildCsv(IEnumerable<MonthlyResourceStatisticsData> rows)
    {
        var builder = new StringBuilder();
        builder.Append("year,month,resourceId,senderVendor,recipientVendor,sender,recipient,totalFileTransfers,uploadCount,totalTransferDownloadAttempts,transfersWithDownloadConfirmed");
        builder.AppendLine();

        foreach (var row in rows)
        {
            builder
                .Append(row.Year).Append(',')
                .Append(row.Month).Append(',')
                .Append(EscapeCsv(row.ResourceId)).Append(',')
                .Append(EscapeCsv(row.SenderSystemVendor)).Append(',')
                .Append(EscapeCsv(row.RecipientSystemVendor)).Append(',')
                .Append(EscapeCsv(row.Sender)).Append(',')
                .Append(EscapeCsv(row.Recipient)).Append(',')
                .Append(row.TotalFileTransfers).Append(',')
                .Append(row.UploadCount).Append(',')
                .Append(row.TotalTransferDownloadAttempts).Append(',')
                .Append(row.TransfersWithDownloadConfirmed);

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string BuildFileName(string? resourceId, DateTime reportMonthStart)
    {
        var resourceSegment = string.IsNullOrWhiteSpace(resourceId)
            ? "all-resources"
            : string.Join("_", resourceId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        return $"monthly_statistics_{resourceSegment}_{reportMonthStart:yyyy-MM}.csv";
    }
}