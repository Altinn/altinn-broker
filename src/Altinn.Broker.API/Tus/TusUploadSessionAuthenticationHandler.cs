using System.Text.Encodings.Web;

using Altinn.Broker.API.Configuration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// Authenticates expired (but otherwise valid) bearer tokens for in-progress TUS uploads.
/// Evaluated before standard JWT bearer so long uploads are not cut off at token lifetime.
/// </summary>
public sealed class TusUploadSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    TusUploadSessionAuthenticationHelper sessionHelper)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = await sessionHelper.TryValidateExpiredTokenForActiveUploadAsync(
            Context,
            Context.RequestAborted);
        if (principal is null)
        {
            return AuthenticateResult.NoResult();
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
