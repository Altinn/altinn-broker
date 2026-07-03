using Microsoft.AspNetCore.Http;

namespace Altinn.Broker.Integrations.Tus;

public static class TusRouteHelper
{
    public const string TusFileIdRouteKey = "TusFileId";
    public const string FileTransferIdItemKey = "TusFileTransferId";
    public const string TusMapPath = "/broker/api/v1/filetransfer/upload/tus";
    public const string PartialPathSegment = "partial";

    private static readonly string PartialPathMarker = $"/{PartialPathSegment}/";

    public static bool TryGetFileTransferIdFromRoute(HttpContext httpContext, out Guid fileTransferId)
    {
        fileTransferId = default;

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

        if (TryParseFileTransferIdFromPartialPath(httpContext.Request.Path.Value, out fileTransferId))
        {
            return true;
        }

        if (IsPartialUploadPath(httpContext.Request.Path.Value))
        {
            return false;
        }

        var routeValue = httpContext.Request.RouteValues[TusFileIdRouteKey]?.ToString();
        if (Guid.TryParse(routeValue, out fileTransferId))
        {
            return true;
        }

        var lastSegment = httpContext.Request.Path.Value?.TrimEnd('/').Split('/').LastOrDefault();
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

        var prefix = $"{TusMapPath}/";
        if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var partialIndex = path.IndexOf(PartialPathMarker, StringComparison.OrdinalIgnoreCase);
        if (partialIndex < prefix.Length)
        {
            return false;
        }

        var fileTransferIdSegment = path[prefix.Length..partialIndex].Trim('/');
        return Guid.TryParse(fileTransferIdSegment, out fileTransferId);
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
