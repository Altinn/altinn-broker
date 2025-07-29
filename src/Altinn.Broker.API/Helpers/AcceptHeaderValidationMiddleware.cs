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
            
            if (producesAttribute != null && acceptHeaders.Length > 0)
            {
                var validMimeTypes = producesAttribute.ContentTypes.ToList();
                validMimeTypes.Add("*/*");

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
        var acceptedMimeTypes = acceptHeader.Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => {
                var parts = x.Split(';');
                return parts[0].Trim().ToLowerInvariant();
            })
            .ToList();
        return acceptedMimeTypes.Any(mimeType => validMimeTypes.Contains(mimeType));
    }
}
