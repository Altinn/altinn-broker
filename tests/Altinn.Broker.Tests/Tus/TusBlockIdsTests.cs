using Altinn.Broker.Integrations.Tus;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusBlockIdsTests
{
    [Fact]
    public void OrderByConcatPosition_SortsByUploadConcatPositionThenChunk()
    {
        var earlyChunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var earlyChunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);
        var lateChunk0 = TusBlockIds.BuildNamespacedBlockId(1, 0);
        var lastCreatedChunk0 = TusBlockIds.BuildNamespacedBlockId(2, 0);
        var lastCreatedChunk1 = TusBlockIds.BuildNamespacedBlockId(2, 1);

        // Azure returns uncommitted blocks in an unspecified order.
        var staged =
            new[]
            {
                lateChunk0,
                lastCreatedChunk1,
                earlyChunk0,
                lastCreatedChunk0,
                earlyChunk1
            };

        // Created as 0, 1, 2 but listed in Upload-Concat as 2, 0, 1.
        var concatPositionByPartialIndex = new Dictionary<int, int>
        {
            [2] = 0,
            [0] = 1,
            [1] = 2
        };

        var ordered = TusBlockIds.OrderByConcatPosition(staged, concatPositionByPartialIndex);

        Assert.Equal(
            [lastCreatedChunk0, lastCreatedChunk1, earlyChunk0, earlyChunk1, lateChunk0],
            ordered);
    }

    [Fact]
    public void OrderByConcatPosition_CreationOrderMatchesList_KeepsChunkOrder()
    {
        var chunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var chunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);
        var nextPartial = TusBlockIds.BuildNamespacedBlockId(1, 0);

        var ordered = TusBlockIds.OrderByConcatPosition(
            [chunk1, nextPartial, chunk0],
            new Dictionary<int, int>
            {
                [0] = 0,
                [1] = 1
            });

        Assert.Equal([chunk0, chunk1, nextPartial], ordered);
    }

    [Fact]
    public void OrderByConcatPosition_PartialMissingFromUploadConcat_Throws()
    {
        var orphan = TusBlockIds.BuildNamespacedBlockId(4, 0);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TusBlockIds.OrderByConcatPosition([orphan], new Dictionary<int, int> { [0] = 0 }));

        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderByConcatPosition_BlockIdIsNotNamespaced_Throws()
    {
        var blockId = Convert.ToBase64String("abc"u8.ToArray());

        Assert.Throws<InvalidOperationException>(() =>
            TusBlockIds.OrderByConcatPosition([blockId], new Dictionary<int, int>()));
    }
}
