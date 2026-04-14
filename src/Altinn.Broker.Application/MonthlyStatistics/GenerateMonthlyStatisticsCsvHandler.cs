using System.Security.Claims;
using System.Text;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.MonthlyStatistics;

public class GenerateMonthlyStatisticsCsvHandler(
    IMonthlyStatisticsRepository monthlyStatisticsRepository,
    IResourceRepository resourceRepository,
    ILogger<GenerateMonthlyStatisticsCsvHandler> logger) : IHandler<GenerateMonthlyStatisticsReportRequest, GenerateMonthlyStatisticsCsvResponse>
{
    public async Task<OneOf<GenerateMonthlyStatisticsCsvResponse, Error>> Process(
        GenerateMonthlyStatisticsReportRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        if (request.Year < 1 || request.Year > 9999 || request.Month < 1 || request.Month > 12 || (request.Year == 9999 && request.Month == 12))
        {
            return StatisticsErrors.InvalidMonthFormat;
        }

        var fromMonthStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusive = fromMonthStart.AddMonths(1);

        var callerOrganizationId = user?.GetCallerOrganizationId();
        if (string.IsNullOrWhiteSpace(callerOrganizationId))
        {
            return Errors.NoAccessToResource;
        }

        logger.LogInformation(
            "Generating monthly statistics CSV for service owner {ServiceOwnerId} for {Year}-{Month}",
            callerOrganizationId.SanitizeForLogs(),
            request.Year,
            request.Month);

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
        {
            var resource = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
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
            resourceId: request.ResourceId,
            cancellationToken: cancellationToken);

        var response = new GenerateMonthlyStatisticsCsvResponse
        {
            Content = Encoding.UTF8.GetBytes(BuildCsv(rows)),
            FileName = BuildFileName(request.ResourceId, fromMonthStart),
            RowCount = rows.Count
        };

        return response;
    }

    private static string BuildCsv(IEnumerable<MonthlyResourceStatisticsData> rows)
    {
        var builder = new StringBuilder();
        builder.Append("year,month,resourceId,sender,recipient,totalFileTransfers,uploadCount,totalTransferDownloadAttempts,transfersWithDownloadConfirmed");
        builder.AppendLine();

        foreach (var row in rows)
        {
            builder
                .Append(row.Year).Append(',')
                .Append(row.Month).Append(',')
                .Append(EscapeCsv(row.ResourceId)).Append(',')
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