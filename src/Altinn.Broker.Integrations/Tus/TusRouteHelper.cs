using Microsoft.AspNetCore.Http;

namespace Altinn.Broker.Integrations.Tus;

public static class TusRouteHelper
{
    public const string TusFileIdRouteKey = "TusFileId";

    public static bool TryGetFileTransferIdFromRoute(HttpContext httpContext, out Guid fileTransferId)
    {
        fileTransferId = default;

        var routeValue = httpContext.Request.RouteValues[TusFileIdRouteKey]?.ToString();
        if (Guid.TryParse(routeValue, out fileTransferId))
        {
            return true;
        }

        var lastSegment = httpContext.Request.Path.Value?.TrimEnd('/').Split('/').LastOrDefault();
        return Guid.TryParse(lastSegment, out fileTransferId);
    }
}
