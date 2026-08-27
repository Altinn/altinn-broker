using Altinn.Broker.API.Configuration;

namespace Altinn.Broker.API.Helpers;

/// <summary>
/// Requires a custom header on state-changing requests authenticated via cookie.
/// Bearer-authenticated requests are exempt.
/// </summary>
public class CsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS"
    };

    public CsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!SafeMethods.Contains(context.Request.Method)
            && context.User.Identity?.IsAuthenticated == true
            && context.User.Identity.AuthenticationType == AuthorizationConstants.EndUserCookie
            && !context.Request.Headers.ContainsKey("X-Requested-With"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Missing X-Requested-With header for cookie-authenticated mutation" });
            return;
        }

        await _next(context);
    }
}
