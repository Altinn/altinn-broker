using StackExchange.Redis;

using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public class RedisTusExpirationDetailsStore(IConnectionMultiplexer redis) : ITusExpirationDetailsStore
{
    private const string ExpirationKey = "broker:tus:expirations";
    private readonly IDatabase _database = redis.GetDatabase();

    public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        => _database.SortedSetAddAsync(ExpirationKey, fileId, expires.ToUnixTimeSeconds());

    public async Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
    {
        var score = await _database.SortedSetScoreAsync(ExpirationKey, fileId);
        return score.HasValue ? DateTimeOffset.FromUnixTimeSeconds((long)score.Value) : null;
    }

    public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
    {
        var values = await _database.SortedSetRangeByScoreAsync(
            ExpirationKey,
            double.NegativeInfinity,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return values.Select(value => value.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>();
    }

    public Task RemoveExpirationAsync(string fileId, CancellationToken cancellationToken)
        => _database.SortedSetRemoveAsync(ExpirationKey, fileId);
}
