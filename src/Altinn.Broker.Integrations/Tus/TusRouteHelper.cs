using Microsoft.AspNetCore.Http;

namespace Altinn.Broker.Integrations.Tus;

public static class TusRouteHelper
{
    public const string TusFileIdRouteKey = "TusFileId";
    public const string FileTransferIdItemKey = "TusFileTransferId";

    public static bool TryGetFileTransferIdFromRoute(HttpContext httpContext, out Guid fileTransferId)
    {
        fileTransferId = default;

        if (httpContext.Items.TryGetValue(FileTransferIdItemKey, out var item)
            && item is string fileTransferIdFromRewrite
            && Guid.TryParse(fileTransferIdFromRewrite, out fileTransferId))
        {
            return true;
        }

        // Prefer our named route parameter when MapTus path includes {fileTransferId}.
        var namedRouteValue = httpContext.Request.RouteValues["fileTransferId"]?.ToString();
        if (Guid.TryParse(namedRouteValue, out fileTransferId))
        {
            return true;
        }

        var routeValue = httpContext.Request.RouteValues[TusFileIdRouteKey]?.ToString();
        if (Guid.TryParse(routeValue, out fileTransferId))
        {
            return true;
        }

        var lastSegment = httpContext.Request.Path.Value?.TrimEnd('/').Split('/').LastOrDefault();
        return Guid.TryParse(lastSegment, out fileTransferId);
    }

    public static string GetRequestPath(HttpContext httpContext)
        => httpContext.Request.Path.Value ?? string.Empty;

    public static bool IsPartialUploadPath(string requestPath)
        => requestPath.Contains("/partial/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// tusdotnet passes concatenation partial ids as "partial/{partialUploadId}" when the upload URL
    /// includes the literal partial segment. Storage always uses the bare partial upload id.
    /// </summary>
    public static string NormalizePartialFileId(string partialFileReference)
    {
        var trimmedReference = partialFileReference.Trim();
        if (!trimmedReference.Contains('/'))
        {
            return trimmedReference;
        }

        return trimmedReference.TrimEnd('/').Split('/').Last();
    }
}
