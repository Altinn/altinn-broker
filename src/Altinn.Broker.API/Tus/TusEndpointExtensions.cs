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
    // fileTransferId must be the last path segment (tusdotnet requirement) and must only appear once (APIM/OpenAPI).
    public const string RouteTemplate = "/broker/api/v1/filetransfer/upload/tus/{fileTransferId}";

    public static WebApplication MapBrokerTusUploads(this WebApplication app)
    {
        app.MapTus(RouteTemplate, httpContext => CreateTusConfiguration(httpContext))
            .RequireAuthorization(AuthorizationConstants.Sender);

        return app;
    }

    private static Task<DefaultTusConfiguration?> CreateTusConfiguration(HttpContext httpContext)
    {
        if (!TryGetFileTransferId(httpContext, out var fileTransferId))
        {
            return Task.FromResult<DefaultTusConfiguration?>(null);
        }

        var tusOptions = httpContext.RequestServices.GetRequiredService<IOptions<TusOptions>>().Value;
        var store = httpContext.RequestServices.GetRequiredService<BrokerTusStore>();

        return Task.FromResult<DefaultTusConfiguration?>(new DefaultTusConfiguration
        {
            Store = store,
            Expiration = new SlidingExpiration(tusOptions.UploadExpiration),
            Events = new Events
            {
                OnAuthorizeAsync = ctx => OnAuthorizeAsync(ctx, fileTransferId),
                OnBeforeCreateAsync = ctx => OnBeforeCreateAsync(ctx, fileTransferId),
                OnCreateCompleteAsync = ctx => OnCreateCompleteAsync(ctx, fileTransferId),
                OnFileCompleteAsync = ctx => OnFileCompleteAsync(ctx, fileTransferId)
            }
        });
    }

    private static bool TryGetFileTransferId(HttpContext httpContext, out Guid fileTransferId)
    {
        fileTransferId = default;
        var routeValue = httpContext.Request.RouteValues["fileTransferId"]?.ToString();
        return Guid.TryParse(routeValue, out fileTransferId);
    }

    private static async Task OnAuthorizeAsync(AuthorizeContext context, Guid fileTransferId)
    {
        var validationService = context.HttpContext.RequestServices.GetRequiredService<TusUploadValidationService>();
        var (_, _, error) = await validationService.ValidateForUploadAsync(
            context.HttpContext.User,
            fileTransferId,
            uploadLength: null,
            context.CancellationToken);

        if (error is not null)
        {
            context.FailRequest(error.StatusCode, error.Message);
        }
    }

    private static async Task OnBeforeCreateAsync(BeforeCreateContext context, Guid fileTransferId)
    {
        if (context.UploadLengthIsDeferred)
        {
            context.FailRequest(HttpStatusCode.BadRequest, "Upload-Defer-Length is not supported");
            return;
        }

        var validationService = context.HttpContext.RequestServices.GetRequiredService<TusUploadValidationService>();
        var (_, maxUploadSize, error) = await validationService.ValidateForUploadAsync(
            context.HttpContext.User,
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

    private static async Task OnCreateCompleteAsync(CreateCompleteContext context, Guid fileTransferId)
    {
        // tusdotnet would otherwise append the file id again, producing .../tus/{id}/{id}.
        context.SetUploadUrl(new Uri(context.HttpContext.Request.Path.Value!, UriKind.Relative));

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

    private static async Task OnFileCompleteAsync(FileCompleteContext context, Guid fileTransferId)
    {
        var tusUploadCompleteHandler = context.HttpContext.RequestServices.GetRequiredService<TusUploadCompleteHandler>();
        var result = await tusUploadCompleteHandler.Process(
            fileTransferId,
            context.HttpContext.User,
            context.CancellationToken);

        if (result.IsT1)
        {
            throw new TusStoreException(result.AsT1.Message);
        }
    }
}
