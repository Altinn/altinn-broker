using Altinn.Broker.API.Tus;

using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Broker.API.Swagger;

/// <summary>
/// Adds TUS resumable upload endpoints to the OpenAPI document.
/// These routes are registered via tusdotnet (<see cref="TusEndpointExtensions"/>) and are not MVC controllers.
/// </summary>
public sealed class TusUploadDocumentFilter : IDocumentFilter
{
    private const string Tag = "FileTransfer";
    private const string TusVersion = "1.0.0";
    private const string TusResumableHeader = "Tus-Resumable";
    private const string UploadLengthHeader = "Upload-Length";
    private const string UploadOffsetHeader = "Upload-Offset";
    private const string OffsetOctetStream = "application/offset+octet-stream";

    private static readonly string TusPath = TusEndpointExtensions.RouteTemplate;

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Paths[TusPath] = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Options] = CreateOptionsOperation(),
                [OperationType.Post] = CreatePostOperation(),
                [OperationType.Head] = CreateHeadOperation(),
                [OperationType.Patch] = CreatePatchOperation(),
                [OperationType.Delete] = CreateDeleteOperation()
            }
        };
    }

    private static OpenApiOperation CreateOptionsOperation() => new()
    {
        Tags = [new OpenApiTag { Name = Tag }],
        Summary = "Discover TUS server capabilities",
        Description = BuildDescription(
            "Returns supported TUS protocol version and extensions. " +
            "Call this before starting a resumable upload. " +
            "See https://tus.io/protocols/resumable-upload.html"),
        OperationId = "TusUploadOptions",
        Parameters = [FileTransferIdParameter()],
        Responses = CreateResponses(
            ("204", "Server capabilities returned in Tus-* response headers"),
            ("401", Unauthorized),
            ("403", Forbidden),
            ("404", NotFound))
    };

    private static OpenApiOperation CreatePostOperation() => new()
    {
        Tags = [new OpenApiTag { Name = Tag }],
        Summary = "Create a resumable upload",
        Description = BuildDescription(
            "Creates a TUS upload resource for an initialized file transfer. " +
            "Requires the <c>Upload-Length</c> header with the total file size in bytes. " +
            "Continue uploading with <c>PATCH</c> and <c>HEAD</c> on this same URL. " +
            "Upload-Defer-Length is not supported."),
        OperationId = "TusUploadCreate",
        Parameters =
        [
            FileTransferIdParameter(),
            TusResumableParameter(required: true),
            UploadLengthParameter(required: true)
        ],
        Responses = CreateResponses(
            ("201", "Upload created. Continue with PATCH on this URL."),
            ("400", BadRequest),
            ("401", Unauthorized),
            ("403", Forbidden),
            ("404", NotFound),
            ("409", Conflict),
            ("413", "File size exceeds maximum"),
            ("503", ServiceUnavailable))
    };

    private static OpenApiOperation CreateHeadOperation() => new()
    {
        Tags = [new OpenApiTag { Name = Tag }],
        Summary = "Get current upload offset",
        Description = BuildDescription(
            "Returns how many bytes have been uploaded so far via the <c>Upload-Offset</c> response header. " +
            "Use this to resume an interrupted upload."),
        OperationId = "TusUploadHead",
        Parameters =
        [
            FileTransferIdParameter(),
            TusResumableParameter(required: true)
        ],
        Responses = CreateResponses(
            ("200", "Current offset returned in Upload-Offset header"),
            ("401", Unauthorized),
            ("403", Forbidden),
            ("404", NotFound))
    };

    private static OpenApiOperation CreatePatchOperation() => new()
    {
        Tags = [new OpenApiTag { Name = Tag }],
        Summary = "Upload the next chunk",
        Description = BuildDescription(
            "Appends a chunk of file data at the offset given in the <c>Upload-Offset</c> request header. " +
            "Repeat until the server offset equals <c>Upload-Length</c>. " +
            "On completion the file is finalized and the file transfer status is updated."),
        OperationId = "TusUploadPatch",
        Parameters =
        [
            FileTransferIdParameter(),
            TusResumableParameter(required: true),
            UploadOffsetParameter(required: true)
        ],
        RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [OffsetOctetStream] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    }
                }
            }
        },
        Responses = CreateResponses(
            ("204", "Chunk accepted. New offset returned in Upload-Offset header."),
            ("400", BadRequest),
            ("401", Unauthorized),
            ("403", Forbidden),
            ("404", NotFound),
            ("409", "Upload-Offset does not match server offset"),
            ("503", ServiceUnavailable))
    };

    private static OpenApiOperation CreateDeleteOperation() => new()
    {
        Tags = [new OpenApiTag { Name = Tag }],
        Summary = "Terminate an incomplete upload",
        Description = BuildDescription(
            "Deletes an in-progress TUS upload. Supported when the termination extension is enabled."),
        OperationId = "TusUploadDelete",
        Parameters =
        [
            FileTransferIdParameter(),
            TusResumableParameter(required: true)
        ],
        Responses = CreateResponses(
            ("204", "Upload terminated"),
            ("401", Unauthorized),
            ("403", Forbidden),
            ("404", NotFound))
    };

    private static string BuildDescription(string body) =>
        $"{body}<br/><br/>One of the scopes:<br/>- altinn:broker.write<br/><br/>" +
        $"Requires the <c>{TusResumableHeader}: {TusVersion}</c> header on every request except OPTIONS.";

    private static OpenApiParameter FileTransferIdParameter() => new()
    {
        Name = "fileTransferId",
        In = ParameterLocation.Path,
        Required = true,
        Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
        Description = "The file transfer id returned from initialize."
    };

    private static OpenApiParameter TusResumableParameter(bool required) => new()
    {
        Name = TusResumableHeader,
        In = ParameterLocation.Header,
        Required = required,
        Schema = new OpenApiSchema { Type = "string", Default = new Microsoft.OpenApi.Any.OpenApiString(TusVersion) },
        Description = "TUS protocol version. Must be 1.0.0."
    };

    private static OpenApiParameter UploadLengthParameter(bool required) => new()
    {
        Name = UploadLengthHeader,
        In = ParameterLocation.Header,
        Required = required,
        Schema = new OpenApiSchema { Type = "integer", Format = "int64" },
        Description = "Total upload size in bytes."
    };

    private static OpenApiParameter UploadOffsetParameter(bool required) => new()
    {
        Name = UploadOffsetHeader,
        In = ParameterLocation.Header,
        Required = required,
        Schema = new OpenApiSchema { Type = "integer", Format = "int64" },
        Description = "Byte offset at which this chunk should be appended."
    };

    private static OpenApiResponses CreateResponses(params (string statusCode, string description)[] responses)
    {
        var result = new OpenApiResponses();
        foreach (var (statusCode, description) in responses)
        {
            result[statusCode] = new OpenApiResponse { Description = description };
        }

        return result;
    }

    private const string Unauthorized =
        "You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry";

    private const string Forbidden =
        "The resource needs to be registered as an Altinn 3 resource and it has to be associated with a service owner";

    private const string NotFound = "The requested file transfer was not found";

    private const string Conflict =
        "A file transfer has already been, or attempted to be, created. Create a new file transfer resource to try again.";

    private const string BadRequest =
        "Service owner needs to be configured, file size exceeds maximum, checksum mismatch, or invalid TUS request";

    private const string ServiceUnavailable = "Storage provider is not ready yet. Please try again later";
}
