using StackExchange.Redis;

using tusdotnet.Interfaces;

namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// Distributed TUS file lock backed by Redis. tusdotnet returns 423 Locked when the lock
/// cannot be acquired, which is expected during concurrent PATCH/HEAD on the same upload.
/// In-memory locks are per-pod only and allow cross-replica races that surface as 409 offsets.
/// </summary>
public sealed class RedisTusFileLockProvider(IConnectionMultiplexer redis) : ITusFileLockProvider
{
    private static readonly TimeSpan LockExpiry = TimeSpan.FromMinutes(15);

    public Task<ITusFileLock> AquireLock(string fileId)
        => Task.FromResult<ITusFileLock>(new RedisTusFileLock(fileId, redis.GetDatabase(), LockExpiry));
}

internal sealed class RedisTusFileLock(string fileId, IDatabase database, TimeSpan lockExpiry) : ITusFileLock
{
    private const string ReleaseLockScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    private readonly string _lockKey = $"tus-upload-lock:{fileId}";
    private readonly string _lockToken = Guid.NewGuid().ToString("N");
    private bool _hasLock;

    public async Task<bool> Lock()
    {
        _hasLock = await database.StringSetAsync(_lockKey, _lockToken, lockExpiry, When.NotExists);
        return _hasLock;
    }

    public async Task ReleaseIfHeld()
    {
        if (!_hasLock)
        {
            return;
        }

        await database.ScriptEvaluateAsync(ReleaseLockScript, [_lockKey], [_lockToken]);
        _hasLock = false;
    }
}
