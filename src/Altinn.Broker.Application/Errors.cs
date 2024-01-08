using System.Net;

namespace Altinn.Broker.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

internal static class Errors
{
    public static Error FileNotFound = new Error(1, "The requested file was not found", HttpStatusCode.NotFound);
    public static Error WrongTokenForSender = new Error(2, "You must use a bearer token that belongs to the sender", HttpStatusCode.Unauthorized);
    public static Error ServiceOwnerNotConfigured = new Error(3, "Service owner needs to be configured to use the broker API", HttpStatusCode.BadRequest);
    public static Error ServiceOwnerNotReadyInfrastructure = new Error(4, "Service owner infrastructure is not ready.", HttpStatusCode.UnprocessableEntity);
    public static Error NoFileUploaded = new Error(5, "No file uploaded yet", HttpStatusCode.BadRequest);
    public static Error FileAlreadyUploaded = new Error(6, "A file has already been, or attempted to be, uploaded. Create a new file resource to try again.", HttpStatusCode.Conflict);
    public static Error ServiceNotConfigured = new Error(7, "Service needs to be configured to use the broker API", HttpStatusCode.Unauthorized);
}
