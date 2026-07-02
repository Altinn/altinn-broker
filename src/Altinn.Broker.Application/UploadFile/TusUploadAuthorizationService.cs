using System.Security.Claims;

using Altinn.Broker.Common;

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
        if (intent is TusUploadAuthIntent.GetInfo)
        {
            return await validationService.ValidateTusGetInfoAsync(
                user,
                fileTransferId,
                cancellationToken);
        }

        var hasCacheKey = TryBuildCacheKey(fileTransferId, user, out var cacheKey);

        if (intent is TusUploadAuthIntent.WriteChunk
            && hasCacheKey
            && await cache.GetStringAsync(cacheKey, cancellationToken) == CacheValue)
        {
            return await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken);
        }

        var (_, _, error) = await validationService.ValidateForUploadAsync(
            user,
            fileTransferId,
            uploadLength,
            isLegacyUser: false,
            cancellationToken);

        if (error is null && intent is not TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await cache.SetStringAsync(
                cacheKey,
                CacheValue,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = GetCacheExpiration()
                },
                cancellationToken);
        }

        if (error is null && intent is TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
        }

        return error;
    }

    public Task InvalidateAsync(Guid fileTransferId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!TryBuildCacheKey(fileTransferId, user, out var cacheKey))
        {
            return Task.CompletedTask;
        }

        return cache.RemoveAsync(cacheKey, cancellationToken);
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

    private TimeSpan GetCacheExpiration()
    {
        var configured = configuration.GetSection("TusOptions:UploadExpiration").Value;
        return TimeSpan.TryParse(configured, out var expiration) ? expiration : TimeSpan.FromHours(24);
    }
}
