using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;
using Parquet.Serialization;
using System.Security.Cryptography;

namespace Altinn.Broker.Application.GenerateReport;

public class GenerateDailySummaryReportHandler(
    IFileTransferRepository fileTransferRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IResourceRepository resourceRepository,
    IAltinnResourceRepository altinnResourceRepository,
    IOptions<ReportStorageOptions> reportStorageOptions,
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

            // Get aggregated data directly from SQL (optimized with GROUP BY)
            // Note: Broker only supports Altinn3, so Altinn2Included is ignored but kept for API compatibility
            var aggregatedData = await fileTransferRepository.GetAggregatedDailySummaryData(cancellationToken);
            logger.LogInformation("Retrieved {count} aggregated records for daily summary report", aggregatedData.Count);

            if (aggregatedData.Count == 0)
            {
                logger.LogWarning("No file transfers found for daily summary report generation");
                return StatisticsErrors.NoFileTransfersFound;
            }

            // Enrich with service owner names and resource titles
            var summaryData = await EnrichAggregatedDataAsync(aggregatedData, cancellationToken);
            logger.LogInformation("Aggregated data into {count} daily summary records", summaryData.Count);

            // Generate parquet file and upload to blob storage
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
                Altinn2Included = false // Broker only supports Altinn3
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
        List<Core.Repositories.AggregatedDailySummaryData> aggregatedData,
        CancellationToken cancellationToken)
    {
        // Fetch service owner names in parallel
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

        // Fetch resource titles in parallel
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

        // Convert to DailySummaryData with enrichment
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

    private async Task<string> GetServiceOwnerIdAsync(FileTransferEntity fileTransfer, CancellationToken cancellationToken)
    {
        // Get service owner from database resource table (service_owner_id_fk -> service_owner.service_owner_id_pk)
        // This matches the mapping: serviceownerorgnr -> service_owner.service_owner_id_pk
        try
        {
            var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
            return resource?.ServiceOwnerId ?? "unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get service owner ID for resource: {ResourceId}", fileTransfer.ResourceId);
            return "unknown";
        }
    }

    private RecipientType GetRecipientType(string recipient)
    {
        if (string.IsNullOrEmpty(recipient) || recipient == "unknown")
        {
            return RecipientType.Unknown;
        }

        string recipientWithoutPrefix = recipient.WithoutPrefix();
        bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
        bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumber();

        if (isOrganization)
        {
            return RecipientType.Organization;
        }
        else if (isPerson)
        {
            return RecipientType.Person;
        }
        else
        {
            return RecipientType.Unknown;
        }
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
            // Get service owner name from Resource Registry (like correspondence does)
            // This returns the name from HasCompetentAuthority.Name (e.g., "Digitaliseringsdirektoratet", "NAV", etc.)
            var serviceOwnerName = await altinnResourceRepository.GetServiceOwnerNameOfResource(resourceId, cancellationToken);
            return serviceOwnerName ?? $"Unknown ({resourceId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get resource title for ID: {resourceId}", resourceId);
            return $"Error ({resourceId})";
        }
    }

    private long CalculateDatabaseStorage(List<FileTransferEntity> fileTransfers)
    {
        // TODO: Calculate database storage based on file transfer metadata
        // For now, return 0 as placeholder
        return 0;
    }

    private long CalculateAttachmentStorage(List<FileTransferEntity> fileTransfers)
    {
        // Sum up file transfer sizes
        return fileTransfers.Sum(ft => ft.FileTransferSize);
    }

    private async Task<(string blobUrl, string fileHash, long fileSize)> GenerateAndUploadParquetFile(List<DailySummaryData> summaryData, bool altinn2Included, CancellationToken cancellationToken)
    {
        // Generate filename with timestamp as prefix and Altinn version indicator
        var altinnVersionIndicator = "A3"; // Broker only supports Altinn3
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

        logger.LogInformation("Generating daily summary parquet file with {count} records for blob storage", summaryData.Count);

        // Generate the parquet file as a stream
        var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, altinn2Included, cancellationToken);

        // Upload to blob storage - need to add this method to storage service
        var blobUrl = await UploadReportFileToStorage(fileName, parquetStream, cancellationToken);

        logger.LogInformation("Successfully generated and uploaded daily summary parquet file to blob storage: {blobUrl}", blobUrl);

        return (blobUrl, fileHash, fileSize);
    }

    private async Task<string> UploadReportFileToStorage(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        // Use Azure Storage directly for reports (similar to correspondence implementation)
        // Reports are stored in a "reports" container in the default storage account
        try
        {
            var connectionString = reportStorageOptions.Value.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ReportStorageOptions.ConnectionString is not configured");
            }
            
            var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("reports");
            
            // Ensure the reports container exists
            await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            var blobClient = blobContainerClient.GetBlobClient(fileName);
            
            // Upload the file
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
            
            logger.LogInformation("Successfully uploaded report file to blob storage: {fileName}", fileName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload report file to blob storage: {fileName}", fileName);
            throw;
        }
    }

    private async Task<(Stream parquetStream, string fileHash, long fileSize)> GenerateParquetFileStream(List<DailySummaryData> summaryData, bool altinn2Included, CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating daily summary parquet file with {count} records", summaryData.Count);

        // Convert to parquet-friendly model
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

        // Create a memory stream for the parquet data
        var memoryStream = new MemoryStream();
        
        // Write parquet data to memory stream
        await ParquetSerializer.SerializeAsync(parquetData, memoryStream, cancellationToken: cancellationToken);
        memoryStream.Position = 0; // Reset position for reading

        // Calculate MD5 hash
        using var md5 = MD5.Create();
        var hash = Convert.ToBase64String(md5.ComputeHash(memoryStream.ToArray()));
        memoryStream.Position = 0; // Reset position for reading

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
            // Get aggregated data directly from SQL (optimized with GROUP BY)
            var aggregatedData = await fileTransferRepository.GetAggregatedDailySummaryData(cancellationToken);
            
            if (!aggregatedData.Any())
            {
                logger.LogWarning("No file transfers found for report generation");
                return StatisticsErrors.NoFileTransfersFound;
            }

            logger.LogInformation("Found {count} aggregated records for report generation", aggregatedData.Count);

            // Enrich with service owner names and resource titles
            var summaryData = await EnrichAggregatedDataAsync(aggregatedData, cancellationToken);

            // Generate the parquet file as a stream
            var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, request.Altinn2Included, cancellationToken);

            // Generate filename
            var altinnVersionIndicator = "A3"; // Broker only supports Altinn3
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
                Altinn2Included = false // Broker only supports Altinn3
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

