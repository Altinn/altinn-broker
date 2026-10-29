using Altinn.Broker.Application.UploadPlans;
using Altinn.Broker.Integrations.Tus;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class UploadPlanStoreTests
{
    [Fact]
    public async Task SaveAndLoadPlan_RoundTrips()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var store = new UploadPlanStore(scope.DistributedCache);
        var fileTransferId = Guid.NewGuid();
        var plan = new UploadPlan(
            "fingerprint",
            1,
            [new UploadPlanPart("part-a", 0, 60), new UploadPlanPart("part-b", 60, 40)],
            DateTimeOffset.UtcNow);

        await store.SaveAsync(fileTransferId, plan, CancellationToken.None);
        var loaded = await store.TryGetAsync(fileTransferId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(plan.Fingerprint, loaded.Fingerprint);
        Assert.Equal(plan.FingerprintVersion, loaded.FingerprintVersion);
        Assert.Equal(plan.Parts, loaded.Parts);
    }

    [Fact]
    public async Task RemovePlan_RemovesSavedPlan()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var store = new UploadPlanStore(scope.DistributedCache);
        var fileTransferId = Guid.NewGuid();
        await store.SaveAsync(
            fileTransferId,
            new UploadPlan("fingerprint", 1, [new UploadPlanPart("part-a", 0, 1)], DateTimeOffset.UtcNow),
            CancellationToken.None);

        await store.RemoveAsync(fileTransferId, CancellationToken.None);

        Assert.Null(await store.TryGetAsync(fileTransferId, CancellationToken.None));
    }
}
