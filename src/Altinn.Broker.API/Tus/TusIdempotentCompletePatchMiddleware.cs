using Altinn.Broker.Integrations.Tus;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// Returns 204 for PATCH requests at a completed upload offset so clients can recover after timeouts
/// without receiving tusdotnet's 400 "Upload is already complete".
/// </summary>
public sealed class TusIdempotentCompletePatchMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPatch(context.Request.Method))
        {
            await next(context);
            return;
        }

        var requestPath = TusRouteHelper.GetRequestPath(context);
        if (!TusRouteHelper.IsTusUploadPath(requestPath)
            || !context.Request.Headers.TryGetValue("Upload-Offset", out var offsetHeader)
            || !long.TryParse(offsetHeader.ToString(), out var patchOffset)
            || !TusRouteHelper.TryGetTusStoreFileIdFromPath(requestPath, out var tusFileId))
        {
            await next(context);
            return;
        }

        if (!TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId)
            && !TusRouteHelper.TryGetFileTransferIdFromPath(requestPath, out fileTransferId))
        {
            await next(context);
            return;
        }

        var store = context.RequestServices.GetRequiredService<BrokerTusStore>();
        var uploadLength = await store.GetUploadLengthAsync(tusFileId, context.RequestAborted);
        if (uploadLength is null or <= 0 || patchOffset < uploadLength.Value)
        {
            await next(context);
            return;
        }

        var length = uploadLength.Value;
        await TusFinalizeRecovery.TryEnqueueFinalizeIfNeededAsync(
            context,
            fileTransferId,
            tusFileId,
            context.RequestAborted);

        context.Response.StatusCode = StatusCodes.Status204NoContent;
        context.Response.Headers["Tus-Resumable"] = "1.0.0";
        context.Response.Headers["Upload-Offset"] = length.ToString();
    }
}
