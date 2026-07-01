using Altinn.Broker.Integrations.Tus;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// tusdotnet appends /{TusFileId?} to the mapped path. When clients call
/// /upload/tus/{fileTransferId}, ASP.NET binds the id as TusFileId which makes tusdotnet
/// treat OPTIONS/POST as file-resource requests and return 404. For those methods we copy
/// the id to fileTransferId and remove TusFileId so tus sees a collection URL.
/// For HEAD/PATCH/DELETE we ensure TusFileId is set from the path id.
/// </summary>
public sealed class TusFileTransferIdRouteMiddleware(RequestDelegate next)
{
    private const string FileTransferIdRouteKey = "fileTransferId";

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsTusEndpoint(context))
        {
            NormalizeRouteValues(context);
        }

        await next(context);
    }

    private static bool IsTusEndpoint(HttpContext context)
        => context.GetEndpoint()?.DisplayName?.StartsWith("tus:", StringComparison.Ordinal) == true;

    private static void NormalizeRouteValues(HttpContext context)
    {
        var tusFileId = context.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey]?.ToString();
        var method = context.Request.Method;

        if (!string.IsNullOrEmpty(tusFileId)
            && (HttpMethods.IsOptions(method) || HttpMethods.IsPost(method)))
        {
            context.Request.RouteValues[FileTransferIdRouteKey] = tusFileId;
            context.Request.RouteValues.Remove(TusRouteHelper.TusFileIdRouteKey);
            return;
        }

        if (string.IsNullOrEmpty(tusFileId)
            && context.Request.RouteValues.TryGetValue(FileTransferIdRouteKey, out var fileTransferId)
            && !string.IsNullOrWhiteSpace(fileTransferId?.ToString()))
        {
            context.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey] = fileTransferId.ToString();
        }
    }
}
