using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using tusdotnet.Interfaces;

namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// Like tusdotnet's in-memory file lock, except that a new request for a locked upload takes the
/// lock over instead of getting 423: it aborts the request holding it and waits for it to let go.
/// A client resuming after a lost connection then doesn't wait for the server to notice that the
/// old request is gone. The IETF resumable uploads draft recommends this (section 4.6), and tusd
/// does the same. Like tusdotnet's lock, it only covers requests on this instance.
/// </summary>
public sealed class TusTakeoverFileLockProvider(
    IHttpContextAccessor httpContextAccessor,
    ILogger<TusTakeoverFileLockProvider> logger,
    TimeSpan? takeoverTimeout = null) : ITusFileLockProvider
{
    private readonly TimeSpan _takeoverTimeout = takeoverTimeout ?? TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, FileLock> _holders = new(StringComparer.OrdinalIgnoreCase);

    public Task<ITusFileLock> AquireLock(string fileId)
        => Task.FromResult<ITusFileLock>(new FileLock(this, fileId, httpContextAccessor.HttpContext));

    private async Task<bool> TakeAsync(FileLock fileLock)
    {
        var deadline = DateTimeOffset.UtcNow + _takeoverTimeout;
        while (true)
        {
            FileLock? holder;
            lock (_holders)
            {
                if (!_holders.TryGetValue(fileLock.FileId, out holder))
                {
                    _holders[fileLock.FileId] = fileLock;
                    return true;
                }

                // Aborted under the lock: a holder that is still registered hasn't finished its
                // request, so its context isn't yet reused for another one.
                holder.Context?.Abort();
            }

            logger.LogInformation(
                "TUS upload {FileId}: a {Method} request is taking over the lock from a {HolderMethod} request that is still running.",
                fileLock.FileId,
                fileLock.Method,
                holder.Method);
            try
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                await holder.Released.Task.WaitAsync(
                    remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                    fileLock.Context?.RequestAborted ?? CancellationToken.None);
            }
            catch (Exception e) when (e is TimeoutException or OperationCanceledException)
            {
                logger.LogWarning(
                    "TUS upload {FileId}: the {HolderMethod} request holding the lock did not let go, so the {Method} request is refused.",
                    fileLock.FileId,
                    holder.Method,
                    fileLock.Method);
                return false;
            }
        }
    }

    private void Release(FileLock fileLock)
    {
        lock (_holders)
        {
            if (_holders.TryGetValue(fileLock.FileId, out var holder) && holder == fileLock)
            {
                _holders.Remove(fileLock.FileId);
            }
        }
        fileLock.Released.TrySetResult();
    }

    private sealed class FileLock(TusTakeoverFileLockProvider provider, string fileId, HttpContext? context) : ITusFileLock
    {
        private bool _hasLock;

        public string FileId { get; } = fileId;

        public HttpContext? Context { get; } = context;

        public string Method { get; } = context?.Request.Method ?? "unknown";

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> Lock()
        {
            if (!_hasLock)
            {
                _hasLock = await provider.TakeAsync(this);
            }
            return _hasLock;
        }

        public Task ReleaseIfHeld()
        {
            if (_hasLock)
            {
                provider.Release(this);
                _hasLock = false;
            }
            return Task.CompletedTask;
        }
    }
}
