using Altinn.Broker.Integrations.Tus;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// Rewrites concatenation partial URLs from
/// /upload/tus/{fileTransferId}/{partialId} to /upload/tus/{partialId}
/// so tusdotnet's single-segment MapTus route can handle PATCH/HEAD/DELETE.
/// The file transfer id is preserved in <see cref="TusRouteHelper.FileTransferIdItemKey"/> for auth and storage resolution.
/// </summary>
public sealed class TusPartialPathRewriteMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        TryRewriteConcatPartialPath(context);
        await next(context);
    }

    public static bool TryRewriteConcatPartialPath(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var prefix = $"{TusEndpointExtensions.TusMapPath}/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = path[prefix.Length..].TrimEnd('/');
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        var fileTransferId = segments[0];
        var partialId = segments[1];
        if (!Guid.TryParse(fileTransferId, out _))
        {
            return false;
        }

        context.Items[TusRouteHelper.FileTransferIdItemKey] = fileTransferId;
        context.Request.Path = new PathString($"{prefix}{partialId}");
        return true;
    }
}
