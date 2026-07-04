using System.Diagnostics;
using System.Net;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;

namespace Altinn.Broker.API.Tus;

public static class TusEndpointExtensions
{
    // OpenAPI/APIM path (fileTransferId is the tus file id in the last segment).
    public const string RouteTemplate = "/broker/api/v1/filetransfer/upload/tus/{fileTransferId}";

    // Concatenation partial uploads use a literal "partial" segment to avoid APIM route ambiguity.
    public const string PartialPathSegment = "partial";
    public const string PartialRouteTemplate =
        $"/broker/api/v1/filetransfer/upload/tus/{{fileTransferId}}/{PartialPathSegment}/{{partialUploadId}}";

    // MapTus appends /{TusFileId?} automatically — do not add a path parameter here.
    public const string TusMapPath = "/broker/api/v1/filetransfer/upload/tus";

    // Route key added by tusdotnet; maps to fileTransferId in our API.
    public const string TusFileIdRouteKey = TusRouteHelper.TusFileIdRouteKey;

    public static WebApplication MapBrokerTusUploads(this WebApplication app)
    {
        app.MapTus(TusMapPath, CreateTusConfiguration)
            .RequireAuthorization(AuthorizationConstants.Sender);

        // Concatenation partial uploads use /tus/{fileTransferId}/partial/{partialId}.
        app.MapTus($"{TusMapPath}/{{fileTransferId}}/{PartialPathSegment}", CreateTusConfiguration)
            .RequireAuthorization(AuthorizationConstants.Sender);

        return app;
    }

    private static Task<DefaultTusConfiguration?> CreateTusConfiguration(HttpContext httpContext)
    {
        var tusOptions = httpContext.RequestServices.GetRequiredService<IOptions<TusOptions>>().Value;
        var store = httpContext.RequestServices.GetRequiredService<BrokerTusStore>();

        return Task.FromResult<DefaultTusConfiguration?>(new DefaultTusConfiguration
        {
            Store = store,
            Expiration = new SlidingExpiration(tusOptions.UploadExpiration),
            Events = new Events
            {
                OnAuthorizeAsync = OnAuthorizeAsync,
                OnBeforeCreateAsync = OnBeforeCreateAsync,
                OnCreateCompleteAsync = OnCreateCompleteAsync,
                OnFileCompleteAsync = OnFileCompleteAsync
            }
        });
    }

    private static async Task<(bool Resolved, Guid FileTransferId)> TryResolveFileTransferIdAsync(
        HttpContext httpContext,
        string? tusFileId,
        CancellationToken cancellationToken)
    {
        var requestPath = TusRouteHelper.GetRequestPath(httpContext);

        if (TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            return (true, fileTransferId);
        }

        var partialUploadRegistry = httpContext.RequestServices.GetRequiredService<ITusPartialUploadRegistry>();
        var normalizedTusFileId = string.IsNullOrWhiteSpace(tusFileId)
            ? tusFileId
            : TusRouteHelper.NormalizePartialFileId(tusFileId);

        if (!string.IsNullOrEmpty(normalizedTusFileId)
            && await partialUploadRegistry.TryGetFileTransferIdAsync(normalizedTusFileId, cancellationToken) is Guid mappedFileTransferId)
        {
            return (true, mappedFileTransferId);
        }

        if (!TusRouteHelper.IsPartialUploadPath(requestPath)
            && !string.IsNullOrEmpty(tusFileId)
            && Guid.TryParse(tusFileId, out fileTransferId))
        {
            return (true, fileTransferId);
        }

        return (false, default);
    }

    private static async Task OnAuthorizeAsync(AuthorizeContext context)
    {
        // TUS OPTIONS is server capability discovery and does not target a file resource.
        if (context.Intent == IntentType.GetOptions)
        {
            return;
        }

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Altinn.Broker.API.Tus.OnAuthorize");
        var sw = logger.IsEnabled(LogLevel.Debug) ? Stopwatch.StartNew() : null;
        long checkpointMs = 0;
        void LogStep(string step, object? detail = null)
        {
            if (sw is null)
            {
                return;
            }

            var totalMs = sw.ElapsedMilliseconds;
            var stepMs = totalMs - checkpointMs;
            checkpointMs = totalMs;
            if (detail is null)
            {
                logger.LogDebug(
                    "TUS timing OnAuthorize {Step} +{StepMs}ms total {TotalMs}ms intent={Intent} fileId={FileId}",
                    step,
                    stepMs,
                    totalMs,
                    context.Intent,
                    context.FileId);
                return;
            }

            logger.LogDebug(
                "TUS timing OnAuthorize {Step} +{StepMs}ms total {TotalMs}ms intent={Intent} fileId={FileId} detail={Detail}",
                step,
                stepMs,
                totalMs,
                context.Intent,
                context.FileId,
                detail);
        }

        LogStep("started");

        var (resolved, fileTransferId) = await TryResolveFileTransferIdAsync(
            context.HttpContext,
            context.FileId,
            context.CancellationToken);
        LogStep("resolveFileTransferId", resolved ? fileTransferId : "notFound");
        if (!resolved)
        {
            context.FailRequest(HttpStatusCode.NotFound, "Missing file transfer id");
            return;
        }

        var authorizationService = context.HttpContext.RequestServices.GetRequiredService<TusUploadAuthorizationService>();
        var error = await authorizationService.AuthorizeAsync(
            context.HttpContext.User,
            fileTransferId,
            MapAuthIntent(context.Intent),
            uploadLength: null,
            context.CancellationToken);
        LogStep("authorize", error is null ? "ok" : "error");

        if (error is not null)
        {
            context.FailRequest(error.StatusCode, error.Message);
        }
    }

    private static async Task OnBeforeCreateAsync(BeforeCreateContext context)
    {
        if (context.FileConcatenation is FileConcatFinal)
        {
            // TUS concatenation final requests must not include Upload-Length.
            var (finalResolved, _) = await TryResolveFileTransferIdAsync(
                context.HttpContext,
                context.FileId,
                context.CancellationToken);
            if (!finalResolved)
            {
                context.FailRequest(HttpStatusCode.NotFound, "Missing file transfer id");
            }

            return;
        }

        if (context.UploadLengthIsDeferred)
        {
            context.FailRequest(HttpStatusCode.BadRequest, "Upload-Defer-Length is not supported");
            return;
        }

        var (resolved, fileTransferId) = await TryResolveFileTransferIdAsync(
            context.HttpContext,
            context.FileId,
            context.CancellationToken);
        if (!resolved)
        {
            context.FailRequest(HttpStatusCode.NotFound, "Missing file transfer id");
            return;
        }

        var validationService = context.HttpContext.RequestServices.GetRequiredService<TusUploadValidationService>();
        var (maxUploadSize, error) = await validationService.ValidateUploadSizeAsync(
            fileTransferId,
            context.UploadLength,
            context.CancellationToken);

        if (error is not null)
        {
            context.FailRequest(error.StatusCode, error.Message);
            return;
        }

        if (maxUploadSize is not null && context.UploadLength > maxUploadSize)
        {
            context.FailRequest(HttpStatusCode.BadRequest, Errors.FileSizeTooBig.Message);
        }
    }

    private static async Task OnCreateCompleteAsync(CreateCompleteContext context)
    {
        var (resolved, fileTransferId) = await TryResolveFileTransferIdAsync(
            context.HttpContext,
            context.FileId,
            context.CancellationToken);
        if (!resolved)
        {
            throw new TusStoreException("Invalid file transfer id");
        }

        var partialUploadRegistry = context.HttpContext.RequestServices.GetRequiredService<ITusPartialUploadRegistry>();
        var partialFileId = TusRouteHelper.NormalizePartialFileId(context.FileId);
        var uploadPath = await partialUploadRegistry.IsPartialAsync(partialFileId, context.CancellationToken)
            ? $"{TusMapPath}/{fileTransferId}/{PartialPathSegment}/{partialFileId}"
            : $"{TusMapPath}/{context.FileId}";
        context.SetUploadUrl(new Uri(uploadPath, UriKind.Relative));

        if (await partialUploadRegistry.IsPartialAsync(partialFileId, context.CancellationToken))
        {
            return;
        }

        var fileTransferStatusRepository = context.HttpContext.RequestServices
            .GetRequiredService<IFileTransferStatusRepository>();
        var uploaderVendor = context.HttpContext.User.GetCallerVendorId()?.WithPrefix();

        await fileTransferStatusRepository.InsertFileTransferStatus(
            fileTransferId,
            FileTransferStatus.UploadStarted,
            timestamp: DateTime.UtcNow,
            vendor: uploaderVendor,
            cancellationToken: context.CancellationToken);
    }

    private static async Task OnFileCompleteAsync(FileCompleteContext context)
    {
        var (resolved, fileTransferId) = await TryResolveFileTransferIdAsync(
            context.HttpContext,
            context.FileId,
            context.CancellationToken);
        if (!resolved)
        {
            throw new TusStoreException("Invalid file transfer id");
        }

        var tusUploadCompleteHandler = context.HttpContext.RequestServices.GetRequiredService<TusUploadCompleteHandler>();
        var result = await tusUploadCompleteHandler.Process(
            fileTransferId,
            context.HttpContext.User,
            context.CancellationToken);

        if (result.IsT1)
        {
            throw new TusStoreException(result.AsT1.Message);
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<BrokerTusStore>();
        await store.CleanupCompletedUploadAsync(fileTransferId.ToString(), context.CancellationToken);

        var authorizationService = context.HttpContext.RequestServices.GetRequiredService<TusUploadAuthorizationService>();
        await authorizationService.InvalidateAsync(fileTransferId, context.HttpContext.User, context.CancellationToken);
    }

    private static TusUploadAuthIntent MapAuthIntent(IntentType intent) => intent switch
    {
        IntentType.CreateFile => TusUploadAuthIntent.Create,
        IntentType.ConcatenateFiles => TusUploadAuthIntent.Create,
        IntentType.WriteFile => TusUploadAuthIntent.WriteChunk,
        IntentType.GetFileInfo => TusUploadAuthIntent.GetInfo,
        IntentType.DeleteFile => TusUploadAuthIntent.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unsupported tus authorization intent.")
    };
}
