using Microsoft.AspNetCore.Http;

namespace Altinn.Broker.Integrations.Tus;

public static class TusRouteHelper
{
    public const string TusFileIdRouteKey = "TusFileId";
    public const string FileTransferIdItemKey = "TusFileTransferId";
    public const string TusMapPath = "/broker/api/v1/filetransfer/upload/tus";
    public const string PartialPathSegment = "partial";

    public static string GetRequestPath(HttpContext httpContext)
        => (httpContext.Request.PathBase + httpContext.Request.Path).ToString();

    public static bool TryGetFileTransferIdFromRoute(HttpContext httpContext, out Guid fileTransferId)
    {
        fileTransferId = default;

        if (httpContext.Items.TryGetValue(FileTransferIdItemKey, out var item)
            && item is string fileTransferIdFromRewrite
            && Guid.TryParse(fileTransferIdFromRewrite, out fileTransferId))
        {
            return true;
        }

        var requestPath = GetRequestPath(httpContext);
        if (TryGetFileTransferIdFromPath(requestPath, out fileTransferId)
            || TryGetFileTransferIdFromPath(httpContext.Request.Path.Value, out fileTransferId))
        {
            return true;
        }

        // Prefer our named route parameter when MapTus path includes {fileTransferId}.
        var namedRouteValue = httpContext.Request.RouteValues["fileTransferId"]?.ToString();
        if (Guid.TryParse(namedRouteValue, out fileTransferId))
        {
            return true;
        }

        // On partial upload URLs the tus file id and last path segment are the partial upload id,
        // not the file transfer id. Never fall back to those for /partial/ paths.
        if (IsPartialUploadPath(requestPath ?? string.Empty))
        {
            return false;
        }

        var routeValue = httpContext.Request.RouteValues[TusFileIdRouteKey]?.ToString();
        if (Guid.TryParse(routeValue, out fileTransferId))
        {
            return true;
        }

        var lastSegment = requestPath?.TrimEnd('/').Split('/').LastOrDefault();
        return Guid.TryParse(lastSegment, out fileTransferId);
    }

    /// <summary>
    /// Extracts the file transfer id from a TUS upload URL without relying on route values.
    /// Required for multi-replica setups where route values may not be bound yet.
    /// </summary>
    public static bool TryGetFileTransferIdFromPath(string? requestPath, out Guid fileTransferId)
    {
        fileTransferId = default;
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        var segments = requestPath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (!string.Equals(segments[i], "tus", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // .../tus/{fileTransferId}/partial/{partialUploadId}
            if (i + 3 < segments.Length
                && string.Equals(segments[i + 2], PartialPathSegment, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[i + 1], out fileTransferId))
            {
                return true;
            }

            // .../tus/{fileTransferId}
            if (i + 1 < segments.Length && Guid.TryParse(segments[i + 1], out fileTransferId))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TrySetFileTransferIdItemFromPartialPath(HttpContext httpContext)
    {
        if (!IsPartialUploadPath(GetRequestPath(httpContext)))
        {
            return false;
        }

        if (!TryGetFileTransferIdFromPath(GetRequestPath(httpContext), out var fileTransferId)
            && !TryGetFileTransferIdFromPath(httpContext.Request.Path.Value, out fileTransferId))
        {
            return false;
        }

        httpContext.Items[FileTransferIdItemKey] = fileTransferId.ToString();
        return true;
    }

    public static bool IsPartialUploadPath(string requestPath)
        => requestPath.Contains("/partial/", StringComparison.OrdinalIgnoreCase);

    public static bool TryGetPartialUploadIdFromPath(string? requestPath, out string partialUploadId)
    {
        partialUploadId = string.Empty;
        if (string.IsNullOrWhiteSpace(requestPath) || !IsPartialUploadPath(requestPath))
        {
            return false;
        }

        partialUploadId = requestPath.TrimEnd('/').Split('/').Last();
        return !string.IsNullOrWhiteSpace(partialUploadId);
    }

    public static bool IsPartialUploadRequest(HttpContext? httpContext, string normalizedFileId)
    {
        if (httpContext is null)
        {
            return false;
        }

        var requestPath = GetRequestPath(httpContext);
        return IsPartialUploadPath(requestPath)
            && TryGetPartialUploadIdFromPath(requestPath, out var pathPartialId)
            && string.Equals(pathPartialId, normalizedFileId, StringComparison.OrdinalIgnoreCase);
    }

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
