using Microsoft.Extensions.Primitives;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", new StringValues("nosniff"));
        context.Response.Headers.Append("Cache-Control", new StringValues("no-store"));

        await next(context);
    }
}