using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace Altinn.Broker.Application.UploadFile;

public enum TusUploadAuthIntent
{
    Create,
    WriteChunk,
    GetInfo,
    Delete
}

public class TusUploadAuthorizationService(
    IDistributedCache cache,
    ITusUploadActivityCache uploadActivityCache,
    TusUploadValidationService validationService,
    IConfiguration configuration)
{
    private const string CacheValue = "1";

    public async Task<Error?> AuthorizeAsync(
        ClaimsPrincipal user,
        Guid fileTransferId,
        TusUploadAuthIntent intent,
        long? uploadLength,
        CancellationToken cancellationToken)
    {
        if (intent is TusUploadAuthIntent.GetInfo or TusUploadAuthIntent.WriteChunk)
        {
            var (handled, activeUploadError) = await TryAuthorizeActiveUploadAsync(
                user,
                fileTransferId,
                cancellationToken);
            if (handled)
            {
                return activeUploadError;
            }
        }

        if (intent is TusUploadAuthIntent.GetInfo)
        {
            return await validationService.ValidateTusGetInfoAsync(
                user,
                fileTransferId,
                cancellationToken);
        }

        var hasCacheKey = TryBuildCacheKey(fileTransferId, user, out var cacheKey);

        var (_, _, error) = await validationService.ValidateForUploadAsync(
            user,
            fileTransferId,
            uploadLength,
            isLegacyUser: false,
            cancellationToken);

        if (error is null && intent is not TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
        }

        if (error is null && intent is TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
        }

        return error;
    }

    public async Task<bool> HasActiveUploadSessionAsync(
        Guid fileTransferId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var (handled, error) = await TryAuthorizeActiveUploadAsync(user, fileTransferId, cancellationToken);
        return handled && error is null;
    }

    public Task InvalidateAsync(Guid fileTransferId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!TryBuildCacheKey(fileTransferId, user, out var cacheKey))
        {
            return Task.CompletedTask;
        }

        return cache.RemoveAsync(cacheKey, cancellationToken);
    }

    private async Task<(bool Handled, Error? Error)> TryAuthorizeActiveUploadAsync(
        ClaimsPrincipal user,
        Guid fileTransferId,
        CancellationToken cancellationToken)
    {
        if (!TryBuildCacheKey(fileTransferId, user, out var cacheKey))
        {
            return (false, null);
        }

        if (await cache.GetStringAsync(cacheKey, cancellationToken) == CacheValue)
        {
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
            return (true, await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken));
        }

        if (!await uploadActivityCache.HasRecentActivityAsync(fileTransferId, GetCacheExpiration(), cancellationToken))
        {
            return (false, null);
        }

        var senderError = await validationService.ValidateActiveUploadSenderAsync(
            user,
            fileTransferId,
            cancellationToken);
        if (senderError is not null)
        {
            return (false, null);
        }

        await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
        return (true, await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken));
    }

    private static bool TryBuildCacheKey(Guid fileTransferId, ClaimsPrincipal user, out string cacheKey)
    {
        var subject = user.FindFirst("sid")?.Value
            ?? user.FindFirst("client_id")?.Value;

        if (string.IsNullOrEmpty(subject))
        {
            cacheKey = string.Empty;
            return false;
        }

        var organization = user.GetCallerOrganizationId() ?? string.Empty;
        cacheKey = $"tus-upload-auth:{fileTransferId}:{subject}:{organization}";
        return true;
    }

    private Task RefreshUploadSessionCacheAsync(string cacheKey, CancellationToken cancellationToken)
        => cache.SetStringAsync(
            cacheKey,
            CacheValue,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = GetCacheExpiration()
            },
            cancellationToken);

    private TimeSpan GetCacheExpiration()
    {
        var configured = configuration.GetSection("TusOptions:UploadExpiration").Value;
        return TimeSpan.TryParse(configured, out var expiration) ? expiration : TimeSpan.FromHours(24);
    }
}
