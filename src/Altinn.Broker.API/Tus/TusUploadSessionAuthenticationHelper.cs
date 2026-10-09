using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.IdPortenDirectAuth;
using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// Allows expired Altinn/Maskinporten tokens (bearer) or expired ID-Porten cookie Altinn JWTs
/// for in-progress TUS uploads when a prior authenticated session is still active in Redis.
/// Clients should still refresh tokens when they can, but long/overnight uploads must not fail
/// solely because access-token or ID-Porten lifetime is shorter than upload duration.
/// </summary>
public sealed class TusUploadSessionAuthenticationHelper(
    IOptionsMonitor<JwtBearerOptions> jwtOptionsMonitor,
    ITusPartialUploadRegistry partialUploadRegistry,
    TusUploadAuthorizationService tusUploadAuthorizationService,
    ILogger<TusUploadSessionAuthenticationHelper> logger)
    : ITusUploadSessionAuthenticationHelper
{
    public async Task<ClaimsPrincipal?> TryValidateExpiredTokenForActiveUploadAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var requestPath = TusRouteHelper.GetRequestPath(httpContext);
        if (!IsTusUploadDataRequest(httpContext.Request))
        {
            return null;
        }

        var token = ExtractBearerToken(httpContext.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogRejection(httpContext, requestPath, "missingBearerToken", fileTransferId: null);
            return null;
        }

        if (!IsExpiredToken(token))
        {
            return null;
        }

        var (principal, validationFailure) = await ValidateTokenWithoutLifetimeAsync(token, cancellationToken);
        if (principal is null)
        {
            LogRejection(httpContext, requestPath, validationFailure ?? "tokenValidationFailed", fileTransferId: null);
            return null;
        }

        return await AcceptIfActiveUploadAsync(
            httpContext,
            requestPath,
            principal,
            p => new ClaimsPrincipal(new ClaimsIdentity(p.Claims, JwtBearerDefaults.AuthenticationScheme)),
            "bearer",
            cancellationToken);
    }

    /// <summary>
    /// Cookie-session counterpart to expired-bearer grace: the Altinn JWT in the Broker session
    /// cookie is expired (or ID-Porten refresh failed), but the TUS upload is still active.
    /// </summary>
    public async Task<ClaimsPrincipal?> TryAcceptExpiredAltinnCookieForActiveUploadAsync(
        HttpContext httpContext,
        string altinnToken,
        string? sid,
        ClaimsPrincipal? existingPrincipal,
        CancellationToken cancellationToken)
    {
        var requestPath = TusRouteHelper.GetRequestPath(httpContext);
        if (!IsTusUploadDataRequest(httpContext.Request))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(altinnToken) || !TryReadJwt(altinnToken, out var jwt) || jwt is null)
        {
            LogRejection(httpContext, requestPath, "missingOrUnreadableAltinnCookieToken", fileTransferId: null);
            return null;
        }

        var identity = new ClaimsIdentity(
            jwt.Claims,
            AuthorizationConstants.EndUserCookie,
            ClaimTypes.Name,
            ClaimTypes.Role);
        if (!string.IsNullOrEmpty(sid) && !identity.HasClaim("sid", sid))
        {
            identity.AddClaim(new Claim("sid", sid));
        }

        var idPortenIdentity = IdPortenPrincipalClaims.CopyIdentity(existingPrincipal);
        var principal = idPortenIdentity is null
            ? new ClaimsPrincipal(identity)
            : new ClaimsPrincipal([identity, idPortenIdentity]);

        return await AcceptIfActiveUploadAsync(
            httpContext,
            requestPath,
            principal,
            p => p,
            AuthorizationConstants.EndUserCookie,
            cancellationToken);
    }

    private async Task<ClaimsPrincipal?> AcceptIfActiveUploadAsync(
        HttpContext httpContext,
        string? requestPath,
        ClaimsPrincipal principal,
        Func<ClaimsPrincipal, ClaimsPrincipal> toAuthenticatedPrincipal,
        string authTypeForLog,
        CancellationToken cancellationToken)
    {
        var fileTransferId = await TryResolveFileTransferIdAsync(httpContext, cancellationToken);
        if (fileTransferId is null)
        {
            LogRejection(httpContext, requestPath, "fileTransferIdNotResolved", fileTransferId: null);
            return null;
        }

        var (isActive, inactiveReason) = await tusUploadAuthorizationService.EvaluateActiveUploadSessionAsync(
            fileTransferId.Value,
            principal,
            cancellationToken);
        if (!isActive)
        {
            LogRejection(
                httpContext,
                requestPath,
                inactiveReason ?? "noActiveUploadSession",
                fileTransferId);
            return null;
        }

        logger.LogInformation(
            "Accepted expired {AuthType} token for active TUS upload. Method={Method} Path={Path} FileTransferId={FileTransferId}",
            authTypeForLog,
            httpContext.Request.Method,
            requestPath,
            fileTransferId);

        return toAuthenticatedPrincipal(principal);
    }

    private static bool TryReadJwt(string token, out JwtSecurityToken? jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            jwt = null;
            return false;
        }

        jwt = handler.ReadJwtToken(token);
        return true;
    }

    private static bool IsTusUploadDataRequest(HttpRequest request)
    {
        var path = TusRouteHelper.GetRequestPath(request.HttpContext);
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!path.Contains("/filetransfer/upload/tus", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPatch(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsDelete(request.Method);
    }

    private static string? ExtractBearerToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        var headerValue = authorizationHeader.ToString();
        if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return headerValue[bearerPrefix.Length..].Trim();
    }

    private static bool IsExpiredToken(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<(ClaimsPrincipal? Principal, string? FailureReason)> ValidateTokenWithoutLifetimeAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var handler = new JwtSecurityTokenHandler();
        string? lastFailure = null;

        foreach (var authenticationScheme in new[]
                 {
                     JwtBearerDefaults.AuthenticationScheme,
                     AuthorizationConstants.LegacyAndMaskinporten
                 })
        {
            var jwtOptions = jwtOptionsMonitor.Get(authenticationScheme);
            var validationParameters = jwtOptions.TokenValidationParameters.Clone();
            validationParameters.ValidateLifetime = false;

            if (jwtOptions.ConfigurationManager is not null)
            {
                try
                {
                    var configuration = await jwtOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);
                    validationParameters.IssuerSigningKeys = configuration.SigningKeys;
                    if (validationParameters.ValidateIssuer)
                    {
                        validationParameters.ValidIssuer = configuration.Issuer;
                    }
                }
                catch (Exception ex) when (ex is InvalidConfigurationException or IOException)
                {
                    lastFailure = $"openidConfigUnavailable:{authenticationScheme}:{ex.GetType().Name}";
                    logger.LogWarning(
                        ex,
                        "Failed to load OpenID configuration for expired TUS token validation. Scheme={Scheme}",
                        authenticationScheme);
                    continue;
                }
            }

            try
            {
                var principal = handler.ValidateToken(token, validationParameters, out _);
                return (principal, null);
            }
            catch (SecurityTokenException ex)
            {
                lastFailure = $"tokenValidationFailed:{authenticationScheme}:{ex.GetType().Name}";
                logger.LogDebug(
                    ex,
                    "Expired TUS token validation failed for scheme {Scheme}",
                    authenticationScheme);
            }
        }

        if (lastFailure is not null)
        {
            logger.LogWarning(
                "Expired TUS token validation failed for all JWT schemes. Failure={Failure}",
                lastFailure);
        }

        return (null, lastFailure);
    }

    private async Task<Guid?> TryResolveFileTransferIdAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var tusFileId = httpContext.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey]?.ToString();
        var normalizedTusFileId = string.IsNullOrWhiteSpace(tusFileId)
            ? null
            : TusRouteHelper.NormalizePartialFileId(tusFileId);

        if (!string.IsNullOrEmpty(normalizedTusFileId))
        {
            var mappedFileTransferId = await partialUploadRegistry.TryGetFileTransferIdAsync(normalizedTusFileId, cancellationToken);
            if (mappedFileTransferId is Guid resolvedFileTransferId)
            {
                return resolvedFileTransferId;
            }
        }

        if (TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            return fileTransferId;
        }

        var requestPath = TusRouteHelper.GetRequestPath(httpContext);
        if (TusRouteHelper.TryGetFileTransferIdFromPath(requestPath, out fileTransferId))
        {
            return fileTransferId;
        }

        if (!TusRouteHelper.IsPartialUploadPath(requestPath)
            && !string.IsNullOrEmpty(normalizedTusFileId)
            && Guid.TryParse(normalizedTusFileId, out fileTransferId))
        {
            return fileTransferId;
        }

        return null;
    }

    private void LogRejection(
        HttpContext httpContext,
        string? requestPath,
        string reason,
        Guid? fileTransferId)
    {
        if (!logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        logger.LogWarning(
            "Rejected expired-token TUS session auth. Reason={Reason} Method={Method} Path={Path} FileTransferId={FileTransferId}",
            reason,
            httpContext.Request.Method,
            requestPath,
            fileTransferId);
    }
}
