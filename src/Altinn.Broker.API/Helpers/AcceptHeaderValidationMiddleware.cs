using Azure.Storage.Blobs.Models;

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
            var acceptHeaders = context.Request.Headers.Accept.ToArray();
            var producesAttribute = endpoint.Metadata
                .OfType<ProducesAttribute>()
                .FirstOrDefault();
            
            if (producesAttribute != null)
            {
                var validMimeTypes = producesAttribute?.ContentTypes.ToList() ?? new List<string>();

                if (acceptHeaders.Length == 0)
                {
                    context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                    await context.Response.WriteAsync("Accept header is required");
                    return;
                }
                if (!acceptHeaders.Any(header => header != null && IsValidAcceptHeader(header, validMimeTypes)))
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
        var AcceptedMimeTypes = acceptHeader.Split(',').Select(x => x.Trim()).ToList();
        return acceptHeader == "*/*" || AcceptedMimeTypes.Any(mimeType => validMimeTypes.Contains(mimeType.Split(';')[0].Trim()));
    }
}
