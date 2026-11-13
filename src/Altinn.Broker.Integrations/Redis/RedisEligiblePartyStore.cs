using Altinn.Broker.Core.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Altinn.Broker.Integrations.Redis;

public class RedisEligiblePartyStore(IConnectionMultiplexer redis, ILogger<RedisEligiblePartyStore> logger) : IEligiblePartyStore
{
    private const string SetKey = "broker:{eligible-parties}:organization-numbers";
    private const string HashKey = "broker:{eligible-parties}:party-uuid-to-organization-number";
    private const string TmpSetKey = SetKey + ":tmp";
    private const string TmpHashKey = HashKey + ":tmp";
    private const int ChunkSize = 1000;


    public async Task<bool> IsEligible(string organizationNumber, CancellationToken cancellationToken)
        => await redis.GetDatabase().SetContainsAsync(SetKey, organizationNumber);

    public async Task ReplaceAll(IReadOnlyDictionary<string, string> partyUuidToOrganizationNumber, CancellationToken cancellationToken)
    {
        var organizationNumbers = partyUuidToOrganizationNumber.Values
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct()
        .ToArray();
        var db = redis.GetDatabase();
        if (organizationNumbers.Length == 0)
        {
            await db.KeyDeleteAsync([SetKey, HashKey, TmpSetKey, TmpHashKey]);
            logger.LogWarning("No eligible parties to store");
            return;
        }

        await db.KeyDeleteAsync([TmpSetKey, TmpHashKey]);
        foreach (var chunk in organizationNumbers.Chunk(ChunkSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await db.SetAddAsync(TmpSetKey, chunk.Select(x => (RedisValue)x).ToArray());
        }
        foreach (var chunk in partyUuidToOrganizationNumber.Chunk(ChunkSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hashEntries = chunk.Select(x => new HashEntry(x.Key, x.Value)).ToArray();
            await db.HashSetAsync(TmpHashKey, hashEntries);
        }
        var transaction = db.CreateTransaction();
        _ = transaction.KeyRenameAsync(TmpSetKey, SetKey);
        _ = transaction.KeyRenameAsync(TmpHashKey, HashKey);
        if (!await transaction.ExecuteAsync())
        {
            throw new Exception("Failed to replace eligible parties in Redis");
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetKnownPartyMappings(IReadOnlyCollection<string> partyUuids, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var known = new Dictionary<string, string>();
        foreach (var chunk in partyUuids.Chunk(ChunkSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = db.HashGetAsync(HashKey, chunk.Select(x => (RedisValue)x).ToArray());
            foreach (var (uuid, value) in chunk.Zip(values.Result))
            {
                if (!value.IsNull)
                {
                    known[uuid] = value.ToString();
                }
            }
        }
        return known;
    }

    public async Task<List<string>> GetAll(CancellationToken cancellationToken)
    {
        var orgNumbers = await redis.GetDatabase().SetMembersAsync(SetKey);
        return orgNumbers.Select(x => x.ToString()).ToList();
    }

}