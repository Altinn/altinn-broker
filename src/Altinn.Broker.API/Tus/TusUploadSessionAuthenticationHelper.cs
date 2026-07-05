using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.Tus;

/// <summary>
/// Allows expired Altinn/Maskinporten tokens for in-progress TUS uploads when a prior
/// authenticated session is still active in Redis. Clients should still refresh tokens,
/// but long uploads must not fail solely because access-token lifetime is shorter than upload duration.
/// </summary>
public sealed class TusUploadSessionAuthenticationHelper(
    IOptionsMonitor<JwtBearerOptions> jwtOptionsMonitor,
    ITusPartialUploadRegistry partialUploadRegistry,
    TusUploadAuthorizationService tusUploadAuthorizationService,
    ILogger<TusUploadSessionAuthenticationHelper> logger)
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

        var authenticatedPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(principal.Claims, JwtBearerDefaults.AuthenticationScheme));

        logger.LogInformation(
            "Accepted expired bearer token for active TUS upload. Method={Method} Path={Path} FileTransferId={FileTransferId}",
            httpContext.Request.Method,
            requestPath,
            fileTransferId);

        return authenticatedPrincipal;
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

        return HttpMethods.IsPatch(request.Method)
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
