using System.Net;

namespace Altinn.Broker.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

public static class Errors
{
    public static Error FileNotFound = new Error(1, "The requested file was not found", HttpStatusCode.NotFound);
    public static Error WrongTokenForSender = new Error(2, "You must use a bearer token that represents the sender of the file", HttpStatusCode.Unauthorized);
    public static Error ResourceOwnerNotConfigured = new Error(3, "Resource owner needs to be configured to use the broker API", HttpStatusCode.BadRequest);
    public static Error ResourceOwnerNotReadyInfrastructure = new Error(4, "Resource owner infrastructure is not ready.", HttpStatusCode.UnprocessableEntity);
    public static Error NoFileUploaded = new Error(5, "No file uploaded yet", HttpStatusCode.BadRequest);
    public static Error FileAlreadyUploaded = new Error(6, "A file has already been, or attempted to be, uploaded. Create a new file resource to try again.", HttpStatusCode.Conflict);
    public static Error ResourceNotConfigured = new Error(7, "Resource needs to be configured to use the broker API", HttpStatusCode.Unauthorized);
    public static Error NoAccessToResource = new Error(8, "You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry", HttpStatusCode.Unauthorized);
    public static Error FileNotAvailable = new Error(9, "The requested file is not ready for download. See file status.", HttpStatusCode.Forbidden);
    public static Error UploadFailed = new Error(10, "Error occurred while uploading file. See /details for more information.", HttpStatusCode.InternalServerError);
    public static Error ChecksumMismatch = new Error(11, "The checksum of uploaded file did not match the checksum specified in initialize call.", HttpStatusCode.BadRequest);
    public static Error FileNotPublished = new Error(12, "A file can only be confirmed to be downloaded when it is published. See file status.", HttpStatusCode.BadRequest);
}
