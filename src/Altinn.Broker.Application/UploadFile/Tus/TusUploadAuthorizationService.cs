using System.Diagnostics;
using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile.Tus;

public enum TusUploadAuthIntent
{
    Create,
    WriteChunk,
    GetInfo,
    Delete
}

public class TusUploadAuthorizationService(
    IDistributedCache distributedCache,
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
            var (handled, activeUploadError, _) = await TryAuthorizeActiveUploadAsync(
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
            cancellationToken);
        timing?.Step("validateForUpload", uploadError is null ? "ok" : "error");

        if (uploadError is null && intent is not TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
            timing?.Step("refreshSessionCache");
        }

        if (uploadError is null && intent is TusUploadAuthIntent.Delete && hasCacheKey)
        {
            await distributedCache.RemoveAsync(cacheKey, cancellationToken);
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
        var (isActive, _) = await EvaluateActiveUploadSessionAsync(fileTransferId, user, cancellationToken);
        return isActive;
    }

    public async Task<(bool IsActive, string? InactiveReason)> EvaluateActiveUploadSessionAsync(
        Guid fileTransferId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var (handled, error, inactiveReason) = await TryAuthorizeActiveUploadAsync(user, fileTransferId, cancellationToken);
        if (!handled)
        {
            return (false, inactiveReason ?? "noActiveUploadSession");
        }

        if (error is not null)
        {
            logger.LogWarning(
                "Active TUS upload session rejected. FileTransferId={FileTransferId} Reason={Reason}",
                fileTransferId,
                error.Message);
            return (false, $"uploadNotInProgress:{error.Message}");
        }

        return (true, null);
    }

    public Task InvalidateAsync(Guid fileTransferId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!TryBuildCacheKey(fileTransferId, user, out var cacheKey))
        {
            return Task.CompletedTask;
        }

        return distributedCache.RemoveAsync(cacheKey, cancellationToken);
    }

    private async Task<(bool Handled, Error? Error, string? InactiveReason)> TryAuthorizeActiveUploadAsync(
        ClaimsPrincipal user,
        Guid fileTransferId,
        CancellationToken cancellationToken,
        TusAuthDebugTiming? timing = null)
    {
        var hasCacheKey = TryBuildCacheKey(fileTransferId, user, out var cacheKey);

        if (hasCacheKey
            && await distributedCache.GetStringAsync(cacheKey, cancellationToken) == CacheValue)
        {
            timing?.Step("activeUpload.sessionCacheHit");
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
            timing?.Step("activeUpload.refreshSessionCache");
            var inProgressError = await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken);
            timing?.Step("activeUpload.validateInProgress", inProgressError is null ? "ok" : "error");
            return (true, inProgressError, null);
        }

        timing?.Step(hasCacheKey ? "activeUpload.sessionCacheMiss" : "activeUpload.noCacheKey");

        if (!await uploadActivityCache.HasRecentActivityAsync(fileTransferId, GetCacheExpiration(), cancellationToken))
        {
            timing?.Step("activeUpload.noRecentActivity");
            return (false, null, hasCacheKey ? "noRecentActivity" : "noSessionCacheKeyOrActivity");
        }

        timing?.Step("activeUpload.recentActivity");

        var senderError = await validationService.ValidateActiveUploadSenderAsync(
            user,
            fileTransferId,
            cancellationToken);
        timing?.Step("activeUpload.validateSender", senderError is null ? "ok" : "error");
        if (senderError is not null)
        {
            return (false, null, "senderMismatch");
        }

        if (hasCacheKey)
        {
            await RefreshUploadSessionCacheAsync(cacheKey, cancellationToken);
            timing?.Step("activeUpload.refreshSessionCache");
        }

        var uploadInProgressError = await validationService.ValidateUploadInProgressAsync(fileTransferId, cancellationToken);
        timing?.Step("activeUpload.validateInProgress", uploadInProgressError is null ? "ok" : "error");
        return (true, uploadInProgressError, null);
    }

    private static bool TryBuildCacheKey(Guid fileTransferId, ClaimsPrincipal user, out string cacheKey)
    {
        var subject = user.FindFirst("sid")?.Value
            ?? user.FindFirst("client_id")?.Value
            ?? user.FindFirst("sub")?.Value;

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
        return distributedCache.SetStringAsync(
            cacheKey,
            CacheValue,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
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
