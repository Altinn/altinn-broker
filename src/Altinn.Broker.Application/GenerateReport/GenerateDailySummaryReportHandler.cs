using System.Security.Cryptography;

using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OneOf;

using Parquet.Serialization;

namespace Altinn.Broker.Application.GenerateReport;

public class GenerateDailySummaryReportHandler(
    IFileTransferRepository fileTransferRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IResourceRepository resourceRepository,
    IAltinnResourceRepository altinnResourceRepository,
    IOptions<ReportStorageOptions> reportStorageOptions,
    IBrokerStorageService brokerStorageService,
    ILogger<GenerateDailySummaryReportHandler> logger,
    IHostEnvironment hostEnvironment)
{
    public async Task<OneOf<GenerateDailySummaryReportResponse, Error>> Process(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting daily summary report generation with Altinn2Included={altinn2Included}", request.Altinn2Included);

            var aggregatedData = await fileTransferRepository.GetAggregatedDailySummaryData(cancellationToken);
            logger.LogInformation("Retrieved {count} aggregated records for daily summary report", aggregatedData.Count);

            if (aggregatedData.Count == 0)
            {
                logger.LogWarning("No file transfers found for daily summary report generation");
                return StatisticsErrors.NoFileTransfersFound;
            }

            var summaryData = await EnrichAggregatedDataAsync(aggregatedData, cancellationToken);
            logger.LogInformation("Aggregated data into {count} daily summary records", summaryData.Count);

            var (blobUrl, fileHash, fileSize) = await GenerateAndUploadParquetFile(summaryData, request.Altinn2Included, cancellationToken);

            var response = new GenerateDailySummaryReportResponse
            {
                FilePath = blobUrl,
                ServiceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count(),
                TotalFileTransferCount = summaryData.Sum(d => d.MessageCount),
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName ?? "Unknown",
                FileSizeBytes = fileSize,
                FileHash = fileHash,
                Altinn2Included = false
            };

            logger.LogInformation("Successfully generated and uploaded daily summary report to blob storage: {blobUrl}", blobUrl);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate daily summary report");
            return StatisticsErrors.ReportGenerationFailed;
        }
    }

    private async Task<List<DailySummaryData>> EnrichAggregatedDataAsync(
        List<AggregatedDailySummaryData> aggregatedData,
        CancellationToken cancellationToken)
    {
        var uniqueServiceOwnerIds = aggregatedData
            .Select(d => d.ServiceOwnerId)
            .Distinct()
            .Where(id => !string.IsNullOrEmpty(id) && id != "unknown")
            .ToList();
        
        var serviceOwnerNames = new Dictionary<string, string>();
        
        var serviceOwnerNameTasks = uniqueServiceOwnerIds.Select(async serviceOwnerId =>
        {
            var name = await GetServiceOwnerNameAsync(serviceOwnerId, cancellationToken);
            return (serviceOwnerId, name);
        });
        
        var serviceOwnerNameResults = await Task.WhenAll(serviceOwnerNameTasks);
        foreach (var (serviceOwnerId, name) in serviceOwnerNameResults)
        {
            serviceOwnerNames[serviceOwnerId] = name;
        }

        var uniqueResourceIds = aggregatedData
            .Select(d => d.ResourceId)
            .Distinct()
            .Where(id => !string.IsNullOrEmpty(id) && id != "unknown")
            .ToList();
        
        var resourceTitles = new Dictionary<string, string>();
        
        var resourceTitleTasks = uniqueResourceIds.Select(async resourceId =>
        {
            var title = await GetResourceTitleAsync(resourceId, cancellationToken);
            return (resourceId, title);
        });
        
        var resourceTitleResults = await Task.WhenAll(resourceTitleTasks);
        foreach (var (resourceId, title) in resourceTitleResults)
        {
            resourceTitles[resourceId] = title;
        }

        return aggregatedData.Select(d => new DailySummaryData
        {
            Date = d.Date,
            Year = d.Year,
            Month = d.Month,
            Day = d.Day,
            ServiceOwnerId = d.ServiceOwnerId,
            ServiceOwnerName = serviceOwnerNames.GetValueOrDefault(d.ServiceOwnerId, "Unknown"),
            ResourceId = d.ResourceId,
            ResourceTitle = resourceTitles.GetValueOrDefault(d.ResourceId, "Unknown"),
            RecipientType = (RecipientType)d.RecipientType,
            AltinnVersion = (AltinnVersion)d.AltinnVersion,
            MessageCount = d.MessageCount,
            DatabaseStorageBytes = d.DatabaseStorageBytes,
            AttachmentStorageBytes = d.AttachmentStorageBytes
        }).ToList();
    }

    private async Task<string> GetServiceOwnerNameAsync(string? serviceOwnerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(serviceOwnerId) || serviceOwnerId == "unknown")
        {
            return "Unknown";
        }

        try
        {
            var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId);
            return serviceOwner?.Name ?? $"Unknown ({serviceOwnerId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get service owner name for ID: {serviceOwnerId}", serviceOwnerId);
            return $"Error ({serviceOwnerId})";
        }
    }

    private async Task<string> GetResourceTitleAsync(string? resourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(resourceId) || resourceId == "unknown")
        {
            return "Unknown";
        }

        try
        {
            var serviceOwnerName = await altinnResourceRepository.GetServiceOwnerNameOfResource(resourceId, cancellationToken);
            return serviceOwnerName ?? $"Unknown ({resourceId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get resource title for ID: {resourceId}", resourceId);
            return $"Error ({resourceId})";
        }
    }

    private async Task<(string blobUrl, string fileHash, long fileSize)> GenerateAndUploadParquetFile(List<DailySummaryData> summaryData, bool altinn2Included, CancellationToken cancellationToken)
    {
        var altinnVersionIndicator = "A3";
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

        logger.LogInformation("Generating daily summary parquet file with {count} records for blob storage", summaryData.Count);

        var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, altinn2Included, cancellationToken);

        var blobUrl = await brokerStorageService.UploadReportFileToStorage(fileName, parquetStream, cancellationToken);

        logger.LogInformation("Successfully generated and uploaded daily summary parquet file to blob storage: {blobUrl}", blobUrl);

        return (blobUrl, fileHash, fileSize);
    }

    private async Task<(Stream parquetStream, string fileHash, long fileSize)> GenerateParquetFileStream(List<DailySummaryData> summaryData, bool altinn2Included, CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating daily summary parquet file with {count} records", summaryData.Count);

        var parquetData = summaryData.Select(d => new ParquetDailySummaryData
        {
            Date = d.Date.ToString("yyyy-MM-dd"),
            Year = d.Year,
            Month = d.Month,
            Day = d.Day,
            ServiceOwnerId = d.ServiceOwnerId,
            ServiceOwnerName = d.ServiceOwnerName,
            ResourceId = d.ResourceId,
            ResourceTitle = d.ResourceTitle,
            RecipientType = d.RecipientType.ToString(),
            AltinnVersion = d.AltinnVersion.ToString(),
            MessageCount = d.MessageCount,
            DatabaseStorageBytes = d.DatabaseStorageBytes,
            AttachmentStorageBytes = d.AttachmentStorageBytes
        }).ToList();

        var memoryStream = new MemoryStream();
        
        await ParquetSerializer.SerializeAsync(parquetData, memoryStream, cancellationToken: cancellationToken);
        memoryStream.Position = 0;

        using var md5 = MD5.Create();
        var hash = Convert.ToBase64String(md5.ComputeHash(memoryStream.ToArray()));
        memoryStream.Position = 0;

        logger.LogInformation("Successfully generated daily summary parquet file stream");

        return (memoryStream, hash, memoryStream.Length);
    }

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> ProcessAndDownload(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting daily summary report generation and download with Altinn2Included={altinn2Included}", request.Altinn2Included);

        try
        {
            var aggregatedData = await fileTransferRepository.GetAggregatedDailySummaryData(cancellationToken);
            
            if (!aggregatedData.Any())
            {
                logger.LogWarning("No file transfers found for report generation");
                return StatisticsErrors.NoFileTransfersFound;
            }

            logger.LogInformation("Found {count} aggregated records for report generation", aggregatedData.Count);

            var summaryData = await EnrichAggregatedDataAsync(aggregatedData, cancellationToken);

            var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, request.Altinn2Included, cancellationToken);

            var altinnVersionIndicator = "A3";
            var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

            var response = new GenerateAndDownloadDailySummaryReportResponse
            {
                FileStream = parquetStream,
                FileName = fileName,
                FileHash = fileHash,
                FileSizeBytes = fileSize,
                ServiceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count(),
                TotalFileTransferCount = summaryData.Sum(d => d.MessageCount),
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName ?? "Unknown",
                Altinn2Included = false
            };

            logger.LogInformation("Successfully generated daily summary report for download with {serviceOwnerCount} service owners and {totalCount} file transfers", 
                response.ServiceOwnerCount, response.TotalFileTransferCount);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate daily summary report for download");
            return StatisticsErrors.ReportGenerationFailed;
        }
    }
}

