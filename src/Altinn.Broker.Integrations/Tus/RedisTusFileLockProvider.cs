using StackExchange.Redis;

using tusdotnet.FileLocks;
using tusdotnet.Interfaces;

namespace Altinn.Broker.Integrations.Tus;

public sealed class RedisTusFileLockProvider(IConnectionMultiplexer? multiplexer) : ITusFileLockProvider
{
    private static readonly TimeSpan LockExpiry = TimeSpan.FromMinutes(5);

    public Task<ITusFileLock> AquireLock(string fileId)
    {
        if (multiplexer is null)
        {
            return InMemoryFileLockProvider.Instance.AquireLock(fileId);
        }

        var normalizedFileId = TusRouteHelper.NormalizePartialFileId(fileId);
        return Task.FromResult<ITusFileLock>(new RedisTusFileLock(multiplexer, normalizedFileId));
    }

    private sealed class RedisTusFileLock(IConnectionMultiplexer multiplexer, string fileId) : ITusFileLock
    {
        private const string ReleaseLockScript =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        private readonly IDatabase _database = multiplexer.GetDatabase();
        private readonly string _lockKey = $"tus-upload-lock:{fileId}";
        private readonly string _lockToken = Guid.NewGuid().ToString("N");
        private bool _hasLock;

        public async Task<bool> Lock()
        {
            if (_hasLock)
            {
                return true;
            }

            _hasLock = await _database.StringSetAsync(
                _lockKey,
                _lockToken,
                LockExpiry,
                When.NotExists);
            return _hasLock;
        }

        public async Task ReleaseIfHeld()
        {
            if (!_hasLock)
            {
                return;
            }

            await _database.ScriptEvaluateAsync(
                ReleaseLockScript,
                [(RedisKey)_lockKey],
                [(RedisValue)_lockToken]);
            _hasLock = false;
        }
    }
}
