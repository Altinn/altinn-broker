using System.Diagnostics;
using System.Security.Claims;

using Altinn.Broker.Application;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public enum TusUploadAuthIntent
{
    Create,
    WriteChunk,
    GetInfo,
    Delete
}

public class TusUploadAuthorizationService(
    HybridCache cache,
    ITusUploadActivityCache uploadActivityCache,
    TusUploadValidationService validationService,
    IConfiguration configuration,
    ILogger<TusUploadAuthorizationService> logger)
{
    private const string CacheValue = "1";

    public async Task<Error?> AuthorizeAsync(
        ClaimsPrincipal user,
        Guid fileTransferId,
        TusUploadAuthIntent intent,
        long? uploadLength,
        CancellationToken cancellationToken)
    {
        TusAuthDebugTiming? timing = logger.IsEnabled(LogLevel.Debug)
            ? new TusAuthDebugTiming(logger, fileTransferId, intent)
            : null;

        if (intent is TusUploadAuthIntent.GetInfo or TusUploadAuthIntent.WriteChunk)
        {
            var (handled, activeUploadError) = await TryAuthorizeActiveUploadAsync(
                user,
                fileTransferId,
                cancellationToken,
                timing);
            if (handled)
            {
                timing?.Step("activeUpload.complete", activeUploadError is null ? "ok" : "error");
                return activeUploadError;
            }

            timing?.Step("activeUpload.notHandled");
        }

        if (intent is TusUploadAuthIntent.GetInfo)
        {
            var getInfoError = await validationService.ValidateTusGetInfoAsync(
                user,
                fileTransferId,
                cancellationToken);
            timing?.Step("validateGetInfo", getInfoError is null ? "ok" : "error");
            return getInfoError;
        }

        var hasCacheKey = TryBuildCacheKey(fileTransferId, user, out var cacheKey);

        var (_, _, uploadError) = await validationService.ValidateForUploadAsync(
            user,
            fileTransferId,
            uploadLength,
            isLegacyUser: false,
            cancellationToken);
        timing?.Step("validateForUpload", uploadError is null ? "ok" : "error");

        if (uploadError is null && intent is not TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
            timing?.Step("refreshSessionCache");
        }

        if (uploadError is null && intent is TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
            timing?.Step("removeSessionCache");
        }

        timing?.Step("complete");
        return uploadError;
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

        return cache.RemoveKeyAsync(cacheKey, cancellationToken);
    }

    private async Task<(bool Handled, Error? Error)> TryAuthorizeActiveUploadAsync(
        ClaimsPrincipal user,
        Guid fileTransferId,
        CancellationToken cancellationToken,
        TusAuthDebugTiming? timing = null)
    {
        if (!TryBuildCacheKey(fileTransferId, user, out var cacheKey))
        {
            timing?.Step("activeUpload.noCacheKey");
            return (false, null);
        }

        if (await cache.GetOptionalStringAsync(cacheKey, cancellationToken: cancellationToken) == CacheValue)
        {
            timing?.Step("activeUpload.sessionCacheHit");
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
            timing?.Step("activeUpload.refreshSessionCache");
            var inProgressError = await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken);
            timing?.Step("activeUpload.validateInProgress", inProgressError is null ? "ok" : "error");
            return (true, inProgressError);
        }

        timing?.Step("activeUpload.sessionCacheMiss");

        if (!await uploadActivityCache.HasRecentActivityAsync(fileTransferId, GetCacheExpiration(), cancellationToken))
        {
            timing?.Step("activeUpload.noRecentActivity");
            return (false, null);
        }

        timing?.Step("activeUpload.recentActivity");

        var senderError = await validationService.ValidateActiveUploadSenderAsync(
            user,
            fileTransferId,
            cancellationToken);
        timing?.Step("activeUpload.validateSender", senderError is null ? "ok" : "error");
        if (senderError is not null)
        {
            return (false, null);
        }

        await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
        timing?.Step("activeUpload.refreshSessionCache");
        var uploadInProgressError = await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken);
        timing?.Step("activeUpload.validateInProgress", uploadInProgressError is null ? "ok" : "error");
        return (true, uploadInProgressError);
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
    {
        var expiration = GetCacheExpiration();
        return cache.SetStringAsync(
            cacheKey,
            CacheValue,
            new HybridCacheEntryOptions
            {
                Expiration = expiration,
                LocalCacheExpiration = expiration
            },
            cancellationToken);
    }

    private TimeSpan GetCacheExpiration()
    {
        var configured = configuration.GetSection("TusOptions:UploadExpiration").Value;
        return TimeSpan.TryParse(configured, out var expiration) ? expiration : TimeSpan.FromHours(24);
    }

    private sealed class TusAuthDebugTiming
    {
        private readonly ILogger _logger;
        private readonly Guid _fileTransferId;
        private readonly TusUploadAuthIntent _intent;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _lastCheckpointMs;

        public TusAuthDebugTiming(ILogger logger, Guid fileTransferId, TusUploadAuthIntent intent)
        {
            _logger = logger;
            _fileTransferId = fileTransferId;
            _intent = intent;
            _logger.LogDebug(
                "TUS timing Authorize started fileTransferId={FileTransferId} intent={Intent}",
                fileTransferId,
                intent);
        }

        public void Step(string step, object? detail = null)
        {
            var totalMs = _stopwatch.ElapsedMilliseconds;
            var stepMs = totalMs - _lastCheckpointMs;
            _lastCheckpointMs = totalMs;

            if (detail is null)
            {
                _logger.LogDebug(
                    "TUS timing Authorize {Step} +{StepMs}ms total {TotalMs}ms fileTransferId={FileTransferId} intent={Intent}",
                    step,
                    stepMs,
                    totalMs,
                    _fileTransferId,
                    _intent);
                return;
            }

            _logger.LogDebug(
                "TUS timing Authorize {Step} +{StepMs}ms total {TotalMs}ms fileTransferId={FileTransferId} intent={Intent} detail={Detail}",
                step,
                stepMs,
                totalMs,
                _fileTransferId,
                _intent,
                detail);
        }
    }
}
