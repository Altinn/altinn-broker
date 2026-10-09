using Altinn.Broker.Core.Domain;
using Altinn.Broker.Integrations.Tus;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusStripeOrderingTests
{
    private sealed record StagedBlock(string BlockId, int StripeIndex, long AbsoluteOffset, int Length);

    private static readonly long[] PartialLengths = [700, 1300, 500, 900];

    private static List<StagedBlock> StageConcatenatedUpload(
        long stripeSize,
        IReadOnlyList<long> partialLengths,
        IReadOnlyList<int> chunkSizes)
    {
        var layout = new StripeLayout(partialLengths.Sum(), stripeSize);
        var staged = new List<StagedBlock>();
        var baseOffset = 0L;

        for (var partialIndex = 0; partialIndex < partialLengths.Count; partialIndex++)
        {
            var partialLength = partialLengths[partialIndex];
            var partialOffset = 0L;
            var blockIndex = 0L;
            var chunkCursor = 0;

            while (partialOffset < partialLength)
            {
                var chunkLength = (int)Math.Min(chunkSizes[chunkCursor % chunkSizes.Count], partialLength - partialOffset);
                chunkCursor++;

                foreach (var fragment in layout.SplitAcrossStripes(baseOffset + partialOffset, chunkLength))
                {
                    staged.Add(new StagedBlock(
                        TusBlockIds.BuildNamespacedBlockId(partialIndex, blockIndex),
                        fragment.StripeIndex,
                        baseOffset + partialOffset + fragment.SourceOffset,
                        fragment.Length));
                    blockIndex++;
                }

                partialOffset += chunkLength;
            }

            baseOffset += partialLength;
        }

        return staged;
    }

    [Theory]
    [InlineData(1024, new[] { 300, 512, 100 })]
    [InlineData(1024, new[] { 333 })]
    [InlineData(64, new[] { 200, 70 })]
    [InlineData(1024, new[] { 256 })]
    public void BlocksWithinAStripeSortByAbsoluteOffset(long stripeSize, int[] chunkSizes)
    {
        // Arrange
        var staged = StageConcatenatedUpload(stripeSize, PartialLengths, chunkSizes);

        // Act
        var stripes = staged
            .GroupBy(block => block.StripeIndex)
            .ToDictionary(
                stripe => stripe.Key,
                stripe => stripe.OrderBy(block => TusBlockIds.TryParseSortableIndex(block.BlockId)).ToList());

        // Assert
        foreach (var (stripeIndex, committed) in stripes)
        {
            Assert.Equal(committed.OrderBy(block => block.AbsoluteOffset).ToList(), committed);

            var expectedOffset = stripeIndex * stripeSize;
            foreach (var block in committed)
            {
                Assert.Equal(expectedOffset, block.AbsoluteOffset);
                expectedOffset += block.Length;
            }
        }
    }

    [Fact]
    public void EveryStripeButTheLastIsExactlyFull()
    {
        // Arrange
        const long stripeSize = 1024;
        var layout = new StripeLayout(PartialLengths.Sum(), stripeSize);

        // Act
        var bytesPerStripe = StageConcatenatedUpload(stripeSize, PartialLengths, [300, 512, 100])
            .GroupBy(block => block.StripeIndex)
            .ToDictionary(stripe => stripe.Key, stripe => stripe.Sum(block => (long)block.Length));

        // Assert
        Assert.Equal(layout.StripeCount, bytesPerStripe.Count);
        for (var stripeIndex = 0; stripeIndex < layout.StripeCount; stripeIndex++)
        {
            Assert.Equal(layout.LengthOfStripe(stripeIndex), bytesPerStripe[stripeIndex]);
        }
    }

    [Fact]
    public void EveryByteOfTheFileIsStagedExactlyOnce()
    {
        // Arrange
        var staged = StageConcatenatedUpload(stripeSize: 1024, PartialLengths, [300, 512, 100]);
        var covered = new bool[PartialLengths.Sum()];

        // Act
        foreach (var block in staged)
        {
            for (var offset = block.AbsoluteOffset; offset < block.AbsoluteOffset + block.Length; offset++)
            {
                Assert.False(covered[offset], $"Byte {offset} was staged more than once");
                covered[offset] = true;
            }
        }

        // Assert
        Assert.DoesNotContain(false, covered);
    }

    [Fact]
    public void BlockIdsAreUniqueWithinEachStripe()
    {
        // Arrange
        var staged = StageConcatenatedUpload(stripeSize: 1024, PartialLengths, [300, 512, 100]);

        // Act
        var blockIdsPerStripe = staged
            .GroupBy(block => block.StripeIndex)
            .Select(stripe => stripe.Select(block => block.BlockId).ToList());

        // Assert
        Assert.All(blockIdsPerStripe, blockIds => Assert.Equal(blockIds.Count, blockIds.Distinct().Count()));
    }
}
