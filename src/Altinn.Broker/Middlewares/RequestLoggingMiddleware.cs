namespace Altinn.Broker.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger
        )
        {
            _next = next;
            _logger = logger;
        }

        private static readonly string[] IgnoredPaths =
        {
            "/health"
        };

        public async Task Invoke(HttpContext httpContext)
        {
            var cancellationToken = httpContext.RequestAborted;

            // Log request
            var requestMethod = httpContext.Request.Method;
            var requestPath = httpContext.Request.PathBase.Add(httpContext.Request.Path).ToString();
            if (!IgnoredPaths.Contains(requestPath.ToLowerInvariant()))
            {
                _logger.LogInformation(
                    "Request for method {RequestMethod} at {RequestPath}",
                    requestMethod,
                    requestPath
                );
            }

            // Facilitate reading response body to allow us to log validation errors
            string responseBody;
            using (var memoryStream = new MemoryStream()) // Need a read-write stream to support reading body later
            {
                var originalResponseStream = httpContext.Response.Body;
                httpContext.Response.Body = memoryStream;
                await _next(httpContext);
                memoryStream.Seek(0, SeekOrigin.Begin);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Request was cancelled");
                    return;
                }
                using (var responseReader = new StreamReader(memoryStream))
                {
                    responseBody = responseReader.ReadToEnd();
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(originalResponseStream, cancellationToken);
                }
                httpContext.Response.Body = originalResponseStream;
            }

            // Log response
            var statusCode = httpContext.Response.StatusCode;
            if (!IgnoredPaths.Contains(requestPath.ToLowerInvariant()))
            {
                if (statusCode >= 200 && statusCode < 400)
                {
                    _logger.LogInformation(
                        "Response for method {RequestMethod} at {RequestPath} with status code {ResponseStatusCode}",
                        requestMethod,
                        requestPath,
                        httpContext.Response.StatusCode
                    );
                }
                else if (statusCode >= 400 && statusCode < 500)
                {
                    _logger.LogWarning(
                        "Response for method {RequestMethod} at {RequestPath} with status code {ResponseStatusCode}. Body was: {ResponseBody}",
                        requestMethod,
                        requestPath,
                        httpContext.Response.StatusCode,
                        responseBody
                    );
                }
                else if (statusCode >= 500)
                {
                    _logger.LogError(
                        "Response for method {RequestMethod} at {RequestPath} with status code {ResponseStatusCode}. Body was: {ResponseBody}",
                        requestMethod,
                        requestPath,
                        httpContext.Response.StatusCode,
                        responseBody
                    );
                }
            }
        }
    }
}
