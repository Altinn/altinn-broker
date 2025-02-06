using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Helpers;

public class AcceptHeaderValidationMiddleware
{
    private readonly RequestDelegate _next;

    public AcceptHeaderValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            var acceptHeader = context.Request.Headers.Accept.ToString();
            var producesAttribute = endpoint.Metadata
                .OfType<ProducesAttribute>()
                .FirstOrDefault();
            
            if (producesAttribute != null)
            {
                var validMimeTypes = producesAttribute?.ContentTypes.ToList() ?? new List<string>();

                if (string.IsNullOrWhiteSpace(acceptHeader))
                {
                    context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                    await context.Response.WriteAsync("Accept header is required");
                    return;
                }
                if (!IsValidAcceptHeader(acceptHeader, validMimeTypes))
                {
                    context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                    await context.Response.WriteAsync($"This endpoint does not support the requested Accept header. Supported Accept headers are: {string.Join(", ", validMimeTypes)}.");
                    return;
                }
            }
        }

        await _next(context);
    }

    private static bool IsValidAcceptHeader(string acceptHeader, IEnumerable<string> validMimeTypes)
    {
        return acceptHeader == "*/*" || validMimeTypes.Any(acceptHeader.Contains);
    }
}
