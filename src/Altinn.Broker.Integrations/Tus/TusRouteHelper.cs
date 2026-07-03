using Microsoft.AspNetCore.Http;

namespace Altinn.Broker.Integrations.Tus;

public static class TusRouteHelper
{
    public const string TusFileIdRouteKey = "TusFileId";
    public const string FileTransferIdItemKey = "TusFileTransferId";
    public const string TusMapPath = "/broker/api/v1/filetransfer/upload/tus";
    public const string PartialPathSegment = "partial";

    private static readonly string PartialPathMarker = $"/{PartialPathSegment}/";

    public static string GetRequestPath(HttpContext httpContext)
    {
        var pathBase = httpContext.Request.PathBase.Value ?? string.Empty;
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (string.IsNullOrEmpty(pathBase))
        {
            return path;
        }

        return $"{pathBase.TrimEnd('/')}{path}";
    }

    public static bool TryGetFileTransferIdFromRoute(HttpContext httpContext, out Guid fileTransferId)
    {
        fileTransferId = default;
        var requestPath = GetRequestPath(httpContext);

        if (httpContext.Items.TryGetValue(FileTransferIdItemKey, out var item)
            && item is string fileTransferIdFromRewrite
            && Guid.TryParse(fileTransferIdFromRewrite, out fileTransferId))
        {
            return true;
        }

        var namedRouteValue = httpContext.Request.RouteValues["fileTransferId"]?.ToString();
        if (Guid.TryParse(namedRouteValue, out fileTransferId))
        {
            return true;
        }

        if (TryParseFileTransferIdFromPartialPath(requestPath, out fileTransferId))
        {
            return true;
        }

        if (IsPartialUploadPath(requestPath))
        {
            return false;
        }

        var routeValue = httpContext.Request.RouteValues[TusFileIdRouteKey]?.ToString();
        if (Guid.TryParse(routeValue, out fileTransferId))
        {
            return true;
        }

        var lastSegment = requestPath.TrimEnd('/').Split('/').LastOrDefault();
        return Guid.TryParse(lastSegment, out fileTransferId);
    }

    public static bool IsPartialUploadPath(string? path)
        => path?.Contains(PartialPathMarker, StringComparison.OrdinalIgnoreCase) == true;

    public static bool TryParseFileTransferIdFromPartialPath(string? path, out Guid fileTransferId)
    {
        fileTransferId = default;
        if (!IsPartialUploadPath(path))
        {
            return false;
        }

        var partialIndex = path!.IndexOf(PartialPathMarker, StringComparison.OrdinalIgnoreCase);
        if (partialIndex <= 0)
        {
            return false;
        }

        var fileTransferIdSegment = path[..partialIndex]
            .TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return Guid.TryParse(fileTransferIdSegment, out fileTransferId);
    }

    public static string GetTusPathRelativeToPathBase(HttpContext httpContext)
    {
        var pathBase = httpContext.Request.PathBase.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(pathBase)
            && TusMapPath.StartsWith(pathBase, StringComparison.OrdinalIgnoreCase))
        {
            return TusMapPath[pathBase.Length..];
        }

        return TusMapPath;
    }

    public static bool TryParsePartialUploadIdFromPath(string? path, out string partialUploadId)
    {
        partialUploadId = string.Empty;
        if (!IsPartialUploadPath(path))
        {
            return false;
        }

        var partialIndex = path!.IndexOf(PartialPathMarker, StringComparison.OrdinalIgnoreCase);
        if (partialIndex < 0)
        {
            return false;
        }

        partialUploadId = path[(partialIndex + PartialPathMarker.Length)..].Trim('/');
        return !string.IsNullOrWhiteSpace(partialUploadId);
    }

    public static bool TryGetPartialUploadContext(
        HttpContext? httpContext,
        string normalizedFileId,
        out Guid fileTransferId,
        out string partialUploadId)
    {
        fileTransferId = default;
        partialUploadId = string.Empty;
        if (httpContext is null)
        {
            return false;
        }

        var requestPath = GetRequestPath(httpContext);
        if (!IsPartialUploadPath(requestPath)
            || !TryParsePartialUploadIdFromPath(requestPath, out partialUploadId)
            || !string.Equals(normalizedFileId, partialUploadId, StringComparison.OrdinalIgnoreCase)
            || !TryGetFileTransferIdFromRoute(httpContext, out fileTransferId))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// tusdotnet passes concatenation partial ids as "partial/{partialUploadId}" when the upload URL
    /// includes the literal partial segment. Storage and Redis always use the bare partial upload id.
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
