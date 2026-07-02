using System.Net;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Options;

using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;

namespace Altinn.Broker.API.Tus;

public static class TusEndpointExtensions
{
    // OpenAPI/APIM path (fileTransferId is the tus file id in the last segment).
    public const string RouteTemplate = "/broker/api/v1/filetransfer/upload/tus/{fileTransferId}";

    // MapTus appends /{TusFileId?} automatically — do not add a path parameter here.
    public const string TusMapPath = "/broker/api/v1/filetransfer/upload/tus";

    // Route key added by tusdotnet; maps to fileTransferId in our API.
    public const string TusFileIdRouteKey = TusRouteHelper.TusFileIdRouteKey;

    public static WebApplication MapBrokerTusUploads(this WebApplication app)
    {
        app.MapTus(TusMapPath, httpContext => CreateTusConfiguration(httpContext))
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

    private static bool TryResolveFileTransferId(HttpContext httpContext, string? tusFileId, out Guid fileTransferId)
    {
        if (TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out fileTransferId))
        {
            return true;
        }

        return Guid.TryParse(tusFileId, out fileTransferId);
    }

    private static async Task OnAuthorizeAsync(AuthorizeContext context)
    {
        // TUS OPTIONS is server capability discovery and does not target a file resource.
        if (context.Intent == IntentType.GetOptions)
        {
            return;
        }

        if (!TryResolveFileTransferId(context.HttpContext, context.FileId, out var fileTransferId))
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

        if (error is not null)
        {
            context.FailRequest(error.StatusCode, error.Message);
        }
    }

    private static async Task OnBeforeCreateAsync(BeforeCreateContext context)
    {
        if (context.UploadLengthIsDeferred)
        {
            context.FailRequest(HttpStatusCode.BadRequest, "Upload-Defer-Length is not supported");
            return;
        }

        if (!TryResolveFileTransferId(context.HttpContext, context.FileId, out var fileTransferId))
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
        var uploadPath = $"{TusMapPath}/{context.FileId}";
        context.SetUploadUrl(new Uri(uploadPath, UriKind.Relative));

        if (!Guid.TryParse(context.FileId, out var fileTransferId))
        {
            throw new TusStoreException("Invalid file transfer id");
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
        if (!Guid.TryParse(context.FileId, out var fileTransferId))
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

        var authorizationService = context.HttpContext.RequestServices.GetRequiredService<TusUploadAuthorizationService>();
        await authorizationService.InvalidateAsync(fileTransferId, context.HttpContext.User, context.CancellationToken);
    }

    private static TusUploadAuthIntent MapAuthIntent(IntentType intent) => intent switch
    {
        IntentType.CreateFile => TusUploadAuthIntent.Create,
        IntentType.WriteFile => TusUploadAuthIntent.WriteChunk,
        IntentType.GetFileInfo => TusUploadAuthIntent.GetInfo,
        IntentType.DeleteFile => TusUploadAuthIntent.Delete,
        _ => TusUploadAuthIntent.WriteChunk
    };
}
