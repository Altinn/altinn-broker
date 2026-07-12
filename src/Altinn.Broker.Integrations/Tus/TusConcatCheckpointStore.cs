using System.Text.Json;

using Altinn.Broker.Application.UploadFile;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusConcatCheckpointStore(IDistributedCache distributedCache) : ITusConcatCheckpointStore
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheExpiration
    };

    private static string CheckpointKey(string tusFileId)
        => $"tus-concat-checkpoint:{TusRouteHelper.NormalizePartialFileId(tusFileId)}";

    public async Task<TusConcatCheckpoint?> TryGetCheckpointAsync(string tusFileId, CancellationToken cancellationToken)
    {
        var json = await distributedCache.GetStringAsync(CheckpointKey(tusFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TusConcatCheckpoint>(json, JsonOptions);
    }

    public Task SaveCheckpointAsync(string tusFileId, TusConcatCheckpoint checkpoint, CancellationToken cancellationToken)
        => distributedCache.SetStringAsync(
            CheckpointKey(tusFileId),
            JsonSerializer.Serialize(checkpoint, JsonOptions),
            CacheOptions,
            cancellationToken);

    public Task ClearCheckpointAsync(string tusFileId, CancellationToken cancellationToken)
        => distributedCache.RemoveAsync(CheckpointKey(tusFileId), cancellationToken);
}
