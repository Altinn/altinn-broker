using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
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
    TusUploadAuthorizationService tusUploadAuthorizationService)
{
    public async Task<ClaimsPrincipal?> TryValidateExpiredTokenForActiveUploadAsync(
        HttpContext httpContext,
        string authenticationScheme,
        CancellationToken cancellationToken)
    {
        if (!IsTusUploadDataRequest(httpContext.Request))
        {
            return null;
        }

        var token = ExtractBearerToken(httpContext.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var principal = ValidateTokenWithoutLifetime(authenticationScheme, token);
        if (principal is null)
        {
            return null;
        }

        var fileTransferId = await TryResolveFileTransferIdAsync(httpContext, cancellationToken);
        if (fileTransferId is null)
        {
            return null;
        }

        var hasActiveSession = await tusUploadAuthorizationService.HasActiveUploadSessionAsync(
            fileTransferId.Value,
            principal,
            cancellationToken);
        return hasActiveSession ? principal : null;
    }

    private static bool IsTusUploadDataRequest(HttpRequest request)
    {
        var path = TusRouteHelper.GetRequestPath(request.HttpContext);
        if (path?.StartsWith(TusRouteHelper.TusMapPath, StringComparison.OrdinalIgnoreCase) != true)
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

    private ClaimsPrincipal? ValidateTokenWithoutLifetime(string authenticationScheme, string token)
    {
        var jwtOptions = jwtOptionsMonitor.Get(authenticationScheme);
        var validationParameters = jwtOptions.TokenValidationParameters.Clone();
        validationParameters.ValidateLifetime = false;

        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
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
        if (TusRouteHelper.TryParseFileTransferIdFromPartialPath(requestPath, out fileTransferId))
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
}
