using Altinn.Broker.Integrations.Tus;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusBlockIdsTests
{
    [Fact]
    public void ExpandByConcatOrder_SortsByUploadConcatPositionThenChunk()
    {
        var earlyChunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var earlyChunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);
        var lateChunk0 = TusBlockIds.BuildNamespacedBlockId(1, 0);
        var lastCreatedChunk0 = TusBlockIds.BuildNamespacedBlockId(2, 0);
        var lastCreatedChunk1 = TusBlockIds.BuildNamespacedBlockId(2, 1);

        // Azure returns uncommitted blocks in an unspecified order.
        var staged = new[]
        {
            lateChunk0,
            lastCreatedChunk1,
            earlyChunk0,
            lastCreatedChunk0,
            earlyChunk1
        };
        var lengths = new long[] { 10, 50, 20, 40, 30 };

        // Created as 0, 1, 2 but listed in Upload-Concat as 2, 0, 1.
        var commitList = TusBlockIds.ExpandByConcatOrder(staged, lengths, [2, 0, 1]);

        Assert.Equal(
            [lastCreatedChunk0, lastCreatedChunk1, earlyChunk0, earlyChunk1, lateChunk0],
            commitList.BlockIds);
        Assert.Equal(40 + 50 + 20 + 30 + 10, commitList.TotalLength);
    }

    [Fact]
    public void ExpandByConcatOrder_RepeatedPartial_EmitsItsBlocksAgain()
    {
        var chunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var chunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);
        var other = TusBlockIds.BuildNamespacedBlockId(1, 0);

        var commitList = TusBlockIds.ExpandByConcatOrder(
            [other, chunk1, chunk0],
            [10, 30, 20],
            [0, 1, 0]);

        Assert.Equal([chunk0, chunk1, other, chunk0, chunk1], commitList.BlockIds);
        Assert.Equal(110, commitList.TotalLength);
    }

    [Fact]
    public void ExpandByConcatOrder_CreationOrderMatchesList_KeepsChunkOrder()
    {
        var chunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var chunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);
        var nextPartial = TusBlockIds.BuildNamespacedBlockId(1, 0);

        var commitList = TusBlockIds.ExpandByConcatOrder(
            [chunk1, nextPartial, chunk0],
            [5, 7, 3],
            [0, 1]);

        Assert.Equal([chunk0, chunk1, nextPartial], commitList.BlockIds);
        Assert.Equal(15, commitList.TotalLength);
    }

    [Fact]
    public void ExpandByConcatOrder_PartialMissingFromUploadConcat_Throws()
    {
        var orphan = TusBlockIds.BuildNamespacedBlockId(4, 0);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TusBlockIds.ExpandByConcatOrder([orphan], [10], [0]));

        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpandByConcatOrder_BlockIdIsNotNamespaced_Throws()
    {
        var blockId = Convert.ToBase64String("abc"u8.ToArray());

        Assert.Throws<InvalidOperationException>(() =>
            TusBlockIds.ExpandByConcatOrder([blockId], [1], []));
    }
}
