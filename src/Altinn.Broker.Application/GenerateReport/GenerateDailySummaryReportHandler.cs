using System.Security.Claims;

using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

using Parquet.Serialization;

namespace Altinn.Broker.Application.GenerateReport;

public class GenerateDailySummaryReportHandler(
    IFileTransferRepository fileTransferRepository,
    IAltinnResourceRepository altinnResourceRepository,
    ILogger<GenerateDailySummaryReportHandler> logger)
{
    /// <summary>
    /// Generates a daily summary report and returns the parquet file stream
    /// </summary>
    public async Task<OneOf<Stream, Error>> Process(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Generating daily summary report for direct download");

            var parquetStream = await GenerateParquetFile(cancellationToken);

            logger.LogInformation("Successfully generated report for download");

            return parquetStream;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating daily summary report for download");
            return Errors.ReportGenerationFailed;
        }
    }

    private async Task<Stream> GenerateParquetFile(CancellationToken cancellationToken)
    {
        // Fetch aggregated data from repository (all data, no filters)
        var summaryData = await fileTransferRepository.GetDailySummaryData(
            fromDate: null,
            toDate: null,
            resourceId: null,
            cancellationToken);

        if (summaryData.Count == 0)
        {
            throw new InvalidOperationException("No file transfers found");
        }

        // Convert to parquet-serializable format
        var parquetData = summaryData.Select(data => new ParquetDailySummaryData
        {
            Date = data.Date.ToString("yyyy-MM-dd"),
            Day = data.Date.Day,
            Month = data.Date.Month,
            Year = data.Date.Year,
            ServiceOwnerId = data.ServiceOwnerId,
            ServiceOwnerName = data.ServiceOwnerName,
            ResourceId = data.ResourceId,
            ResourceTitle = GetResourceTitle(data.ResourceId),
            RecipientType = "Organization",
            AltinnVersion = "Altinn3",
            FileTransferCount = data.FileTransferCount,
            DatabaseStorageBytes = 0, // Not tracked for file transfers
            AttachmentStorageBytes = 0 // Not tracked for file transfers
        }).ToList();

        // Generate parquet file in memory
        var memoryStream = new MemoryStream();
        await ParquetSerializer.SerializeAsync(parquetData, memoryStream, cancellationToken: cancellationToken);
        
        memoryStream.Position = 0;

        logger.LogInformation("Generated parquet file with {RecordCount} records", parquetData.Count);

        return memoryStream;
    }

    private string GetResourceTitle(string? resourceId)
    {
        if (string.IsNullOrEmpty(resourceId) || resourceId == "unknown")
        {
            return "Unknown";
        }

        try
        {
            var resourceTitle = altinnResourceRepository.GetServiceOwnerNameOfResource(resourceId, CancellationToken.None).GetAwaiter().GetResult();
            return resourceTitle ?? $"Unknown ({resourceId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get resource title for ID: {resourceId}", resourceId);
            return $"Error ({resourceId})";
        }
    }
}
