using System.Text.Json;

using Altinn.Broker.Application.UploadPlans;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public sealed class UploadPlanStore(IDistributedCache distributedCache) : IUploadPlanStore
{
    // Longer than the tus uploads it describes live. Whether a plan can still be resumed is decided by
    // the transfer's status and its uploads, and the plan is removed when the transfer is done with it.
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheExpiration
    };

    private static string PlanKey(Guid fileTransferId) => $"tus-upload-plan:{fileTransferId:N}";

    public async Task<UploadPlan?> TryGetAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var json = await distributedCache.GetStringAsync(PlanKey(fileTransferId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<UploadPlan>(json, JsonOptions);
    }

    public Task SaveAsync(Guid fileTransferId, UploadPlan plan, CancellationToken cancellationToken)
        => distributedCache.SetStringAsync(
            PlanKey(fileTransferId),
            JsonSerializer.Serialize(plan, JsonOptions),
            CacheOptions,
            cancellationToken);

    public Task RemoveAsync(Guid fileTransferId, CancellationToken cancellationToken)
        => distributedCache.RemoveAsync(PlanKey(fileTransferId), cancellationToken);
}
