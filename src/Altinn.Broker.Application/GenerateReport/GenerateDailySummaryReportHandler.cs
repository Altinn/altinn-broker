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
    IBrokerStorageService storageService,
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

            // Get file transfers for statistics
            // Note: Broker only supports Altinn3, so Altinn2Included is ignored but kept for API compatibility
            var fileTransfers = await fileTransferRepository.GetFileTransfersForReport(cancellationToken);
            logger.LogInformation("Retrieved {count} file transfers for daily summary report", fileTransfers.Count);

            if (fileTransfers.Count == 0)
            {
                logger.LogWarning("No file transfers found for daily summary report generation");
                return StatisticsErrors.NoFileTransfersFound;
            }
            // Aggregate daily data
            var summaryData = AggregateDailyData(fileTransfers);
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

    private List<DailySummaryData> AggregateDailyData(List<FileTransferEntity> fileTransfers)
    {
        // Flatten file transfers with recipients - each recipient gets its own row
        var flattenedData = new List<(FileTransferEntity ft, string recipientId)>();
        
        foreach (var ft in fileTransfers)
        {
            if (ft.RecipientCurrentStatuses.Any())
            {
                foreach (var recipient in ft.RecipientCurrentStatuses)
                {
                    flattenedData.Add((ft, recipient.Actor.ActorExternalId));
                }
            }
            else
            {
                // If no recipients, still include the file transfer with unknown recipient
                flattenedData.Add((ft, "unknown"));
            }
        }

        var groupedData = flattenedData
            .GroupBy(item => new
            {
                item.ft.Created.Date,
                ServiceOwnerId = GetServiceOwnerId(item.ft),
                ResourceId = item.ft.ResourceId ?? "unknown",
                RecipientId = item.recipientId,
                RecipientType = GetRecipientType(item.recipientId),
                AltinnVersion = AltinnVersion.Altinn3 // Broker only supports Altinn3
            })
            .Select(g => new DailySummaryData
            {
                Date = g.Key.Date,
                Year = g.Key.Date.Year,
                Month = g.Key.Date.Month,
                Day = g.Key.Date.Day,
                ServiceOwnerId = g.Key.ServiceOwnerId,
                ServiceOwnerName = GetServiceOwnerName(g.Key.ServiceOwnerId),
                ResourceId = g.Key.ResourceId,
                ResourceTitle = GetResourceTitle(g.Key.ResourceId),
                RecipientType = g.Key.RecipientType,
                AltinnVersion = g.Key.AltinnVersion,
                MessageCount = g.Count(),
                DatabaseStorageBytes = CalculateDatabaseStorage(g.Select(x => x.ft).ToList()),
                AttachmentStorageBytes = CalculateAttachmentStorage(g.Select(x => x.ft).ToList())
            })
            .OrderBy(d => d.Date)
            .ThenBy(d => d.ServiceOwnerId)
            .ThenBy(d => d.ResourceId)
            .ThenBy(d => d.RecipientType)
            .ThenBy(d => d.AltinnVersion)
            .ToList();

        return groupedData;
    }

    private string GetServiceOwnerId(FileTransferEntity fileTransfer)
    {
        // Get service owner from database resource table (service_owner_id_fk -> service_owner.service_owner_id_pk)
        // This matches the mapping: serviceownerorgnr -> service_owner.service_owner_id_pk
        try
        {
            var resource = resourceRepository.GetResource(fileTransfer.ResourceId, CancellationToken.None).GetAwaiter().GetResult();
            return resource?.ServiceOwnerId ?? "unknown";
        }
        catch
        {
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

    private string GetServiceOwnerName(string? serviceOwnerId)
    {
        if (string.IsNullOrEmpty(serviceOwnerId) || serviceOwnerId == "unknown")
        {
            return "Unknown";
        }

        try
        {
            var serviceOwner = serviceOwnerRepository.GetServiceOwner(serviceOwnerId).GetAwaiter().GetResult();
            return serviceOwner?.Name ?? $"Unknown ({serviceOwnerId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get service owner name for ID: {serviceOwnerId}", serviceOwnerId);
            return $"Error ({serviceOwnerId})";
        }
    }

    private string GetResourceTitle(string? resourceId)
    {
        if (string.IsNullOrEmpty(resourceId) || resourceId == "unknown")
        {
            return "Unknown";
        }

        try
        {
            // Get service owner name from Resource Registry (like correspondence does)
            // This returns the name from HasCompetentAuthority.Name (e.g., "Digitaliseringsdirektoratet", "NAV", etc.)
            var serviceOwnerName = altinnResourceRepository.GetServiceOwnerNameOfResource(resourceId, CancellationToken.None).GetAwaiter().GetResult();
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
            // Get file transfers data
            var fileTransfers = await fileTransferRepository.GetFileTransfersForReport(cancellationToken);
            
            if (!fileTransfers.Any())
            {
                logger.LogWarning("No file transfers found for report generation");
                return StatisticsErrors.NoFileTransfersFound;
            }

            logger.LogInformation("Found {count} file transfers for report generation", fileTransfers.Count);

            // Aggregate data by day and service owner
            var summaryData = AggregateDailyData(fileTransfers);

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
                TotalFileTransferCount = fileTransfers.Count,
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

