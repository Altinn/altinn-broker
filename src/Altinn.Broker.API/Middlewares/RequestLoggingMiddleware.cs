namespace Altinn.Broker.Middlewares;

public class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger
    )
{
    private static readonly string[] IgnoredPaths =
    {
        "/health",
        "/hangfire"
    };

    public async Task Invoke(HttpContext httpContext)
    {
        // Log request
        var requestMethod = httpContext.Request.Method;
        var requestPath = httpContext.Request.PathBase.Add(httpContext.Request.Path).ToString();
        if (!IgnoredPaths.Any(path => requestPath.ToLowerInvariant().StartsWith(path)))
        {
            logger.LogInformation(
                "Request for method {RequestMethod} at {RequestPath}",
                requestMethod,
                requestPath
            );
        }

        await next(httpContext);

        // Log response
        var statusCode = httpContext.Response.StatusCode;
        if (!IgnoredPaths.Any(path => requestPath.ToLowerInvariant().StartsWith(path)))
        {
            if (statusCode >= 200 && statusCode < 400)
            {
                logger.LogInformation(
                    "Response for method {RequestMethod} at {RequestPath} with status code {ResponseStatusCode}",
                    requestMethod,
                    requestPath,
                    httpContext.Response.StatusCode
                );
            }
            else if (statusCode >= 400 && statusCode < 500)
            {
                logger.LogWarning(
                    "Response for method {RequestMethod} at {RequestPath} with status code {ResponseStatusCode}",
                    requestMethod,
                    requestPath,
                    httpContext.Response.StatusCode
                );
            }
            else if (statusCode >= 500)
            {
                logger.LogError(
                    "Response for method {RequestMethod} at {RequestPath} with status code {ResponseStatusCode}",
                    requestMethod,
                    requestPath,
                    httpContext.Response.StatusCode
                );
            }
        }
    }
}
