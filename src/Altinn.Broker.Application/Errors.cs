using System.Net;

namespace Altinn.Broker.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

public static class Errors
{
    public static Error FileTransferNotFound = new Error(1, "The requested file transfer was not found", HttpStatusCode.NotFound);
    public static Error ServiceOwnerNotConfigured = new Error(2, "Service owner needs to be configured to use the broker API", HttpStatusCode.BadRequest);
    public static Error NoFileUploaded = new Error(3, "No file uploaded yet", HttpStatusCode.BadRequest);
    public static Error FileTransferAlreadyUploaded = new Error(4, "A file transfer has already been, or attempted to be, created. Create a new file transfer resource to try again.", HttpStatusCode.Conflict);
    public static Error InvalidResourceDefinition = new Error(5, "The resource needs to be registered as an Altinn 3 resource and it has to be associated with a service owner", HttpStatusCode.Forbidden);
    public static Error NoAccessToResource = new Error(6, "You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry", HttpStatusCode.Unauthorized);
    public static Error FileTransferNotAvailable = new Error(7, "The requested file transfer's file is not ready for download. See file transfer status.", HttpStatusCode.Forbidden);
    public static Error UploadFailed = new Error(8, "Error occurred while uploading file See /details for more information.", HttpStatusCode.InternalServerError);
    public static Error ChecksumMismatch = new Error(9, "The checksum of uploaded file did not match the checksum specified in initialize call.", HttpStatusCode.BadRequest);
    public static Error FileTransferNotPublished = new Error(10, "A file transfer can only be confirmed to be downloaded when it is published. See file transfer status.", HttpStatusCode.BadRequest);
    public static Error MaxUploadSizeCannotBeNegative = new Error(11, "Max file transfer size cannot be negative", HttpStatusCode.BadRequest);
    public static Error MaxUploadSizeCannotBeZero = new Error(12, "Max file transfer size cannot be zero", HttpStatusCode.BadRequest);
    public static Error MaxUploadSizeForVirusScan = new Error(13, "Max file transfer size cannot be set higher than the 2GB in production unless the resource has been pre-approved for disabled virus scan. Contact us @ Slack.", HttpStatusCode.BadRequest);
    public static Error InvalidTimeToLiveFormat = new Error(14, "Invalid file transfer time to live format. Should follow ISO8601 standard for duration. Example: 'P30D' for 30 days.", HttpStatusCode.BadRequest);
    public static Error TimeToLiveCannotExceed365Days = new Error(15, "Time to live cannot exceed 365 days", HttpStatusCode.BadRequest);
    public static Error FileSizeTooBig = new Error(16, "File size exceeds maximum", HttpStatusCode.BadRequest);
    public static Error InvalidGracePeriodFormat = new Error(17, "Invalid grace period format. Should follow ISO8601 standard for duration. Example: 'PT2H' for 2 hours.", HttpStatusCode.BadRequest);
    public static Error GracePeriodCannotExceed24Hours = new Error(18, "Grace period cannot exceed 24 hours", HttpStatusCode.BadRequest);
    public static Error ConfirmDownloadBeforeDownloadStarted = new Error(19, "Cannot confirm before the files have been downloaded", HttpStatusCode.BadRequest);
    public static Error NotApprovedForDisabledVirusScan = new Error(20, "In order to use file transfers without virus scan the service resource needs to be approved by Altinn. Please contact us @ Slack.", HttpStatusCode.BadRequest);
    public static Error StorageProviderNotReady = new Error(21, "Storage provider is not ready yet. Please try again later.", HttpStatusCode.ServiceUnavailable);
    public static Error MaxUploadSizeOverGlobal = new Error(22, "Max file transfer size cannot be set higher than 100GB in production because it has not yet been tested for it. Contact us @ Slack if you need it.", HttpStatusCode.BadRequest);
    public static Error NeedServiceCodeForManifestShim = new Error(23, "In order to use manifest file shim you need to provide external service code and edition code", HttpStatusCode.BadRequest);
}

public static class StatisticsErrors
{
    public static Error NoFileTransfersFound = new Error(6001, "No file transfers found for report generation", HttpStatusCode.NotFound);
    public static Error ReportGenerationFailed = new Error(6002, "Failed to generate statistics report", HttpStatusCode.InternalServerError);
}
