using Altinn.Broker.Integrations.Tus;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// APIM and other reverse proxies may set <see cref="Microsoft.AspNetCore.Http.HttpRequest.PathBase"/>
/// (for example /broker/api/v1) while <see cref="Microsoft.AspNetCore.Http.HttpRequest.Path"/> only
/// contains the suffix (/filetransfer/upload/tus/...). ASP.NET route matching uses Path, so MapTus
/// templates that include the full /broker/api/v1 prefix would not match unless the paths are merged.
/// </summary>
public sealed class TusPathBaseNormalizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        NormalizeTusPath(context);
        await next(context);
    }

    internal static void NormalizeTusPath(HttpContext context)
    {
        var pathBase = context.Request.PathBase.Value ?? string.Empty;
        var path = context.Request.Path.Value ?? string.Empty;
        if (string.IsNullOrEmpty(pathBase) || string.IsNullOrEmpty(path))
        {
            return;
        }

        if (path.StartsWith(TusRouteHelper.TusMapPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/broker/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var combinedPath = $"{pathBase.TrimEnd('/')}{path}";
        if (!combinedPath.Contains("/filetransfer/upload/tus", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Request.Path = new PathString(combinedPath);
        context.Request.PathBase = PathString.Empty;
    }
}
