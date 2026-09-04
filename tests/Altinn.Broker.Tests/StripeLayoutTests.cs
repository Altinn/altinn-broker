using Altinn.Broker.Core.Domain;

using Xunit;

namespace Altinn.Broker.Tests;

public class StripeLayoutTests
{
    private const long GiB = 1024L * 1024 * 1024;
    private const long TiB = 1024L * GiB;

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1000, 0, 1)]
    [InlineData(0, 100, 1)]
    [InlineData(99, 100, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(101, 100, 2)]
    [InlineData(200, 100, 2)]
    [InlineData(500, 100, 5)]
    [InlineData(501, 100, 6)]
    public void StripeCount_ReturnsExpected(long totalLength, long stripeSize, int expected)
    {
        // Arrange
        var layout = new StripeLayout(totalLength, stripeSize);

        // Act
        var stripeCount = layout.StripeCount;

        // Assert
        Assert.Equal(expected, stripeCount);
    }

    [Theory]
    [InlineData(100, 100, false)]
    [InlineData(101, 100, true)]
    [InlineData(1000, 0, false)]
    [InlineData(0, 100, false)]
    public void IsStriped_OnlyWhenContentSpansMoreThanOneBlob(long totalLength, long stripeSize, bool expected)
    {
        // Arrange
        var layout = new StripeLayout(totalLength, stripeSize);

        // Act
        var isStriped = layout.IsStriped;

        // Assert
        Assert.Equal(expected, isStriped);
    }

    [Fact]
    public void LengthOfStripe_EveryStripeButTheLastIsFull()
    {
        // Arrange
        var layout = new StripeLayout(TotalLength: 250, StripeSize: 100);

        // Act
        var lengths = new[] { layout.LengthOfStripe(0), layout.LengthOfStripe(1), layout.LengthOfStripe(2) };

        // Assert
        Assert.Equal(3, layout.StripeCount);
        Assert.Equal([100L, 100L, 50L], lengths);
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.LengthOfStripe(3));
    }

    [Fact]
    public void LengthOfStripe_ExactMultiple_LastStripeIsFull()
    {
        // Arrange
        var layout = new StripeLayout(TotalLength: 300, StripeSize: 100);

        // Act
        var lastStripeLength = layout.LengthOfStripe(2);

        // Assert
        Assert.Equal(3, layout.StripeCount);
        Assert.Equal(100, lastStripeLength);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(99, 0, 99)]
    [InlineData(100, 1, 0)]
    [InlineData(250, 2, 50)]
    public void StripeIndexOf_AndOffsetWithinStripe_AreDerivedFromTheOffsetAlone(
        long absoluteOffset, int expectedIndex, long expectedOffsetWithin)
    {
        // Arrange
        var layoutWithUnknownTotalLength = new StripeLayout(TotalLength: 0, StripeSize: 100);

        // Act
        var stripeIndex = layoutWithUnknownTotalLength.StripeIndexOf(absoluteOffset);
        var offsetWithinStripe = layoutWithUnknownTotalLength.OffsetWithinStripe(absoluteOffset);

        // Assert
        Assert.Equal(expectedIndex, stripeIndex);
        Assert.Equal(expectedOffsetWithin, offsetWithinStripe);
    }

    [Fact]
    public void SplitAcrossStripes_ChunkInsideOneStripe_IsNotSplit()
    {
        // Arrange
        var layout = new StripeLayout(0, 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 10, length: 20);

        // Assert
        Assert.Equal([new StripeFragment(0, 10, 0, 20)], fragments);
    }

    [Fact]
    public void SplitAcrossStripes_ChunkEndingExactlyOnABoundary_IsNotSplit()
    {
        // Arrange
        var layout = new StripeLayout(0, 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 80, length: 20);

        // Assert
        Assert.Equal([new StripeFragment(0, 80, 0, 20)], fragments);
    }

    [Fact]
    public void SplitAcrossStripes_ChunkStartingExactlyOnABoundary_IsNotSplit()
    {
        // Arrange
        var layout = new StripeLayout(0, 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 100, length: 20);

        // Assert
        Assert.Equal([new StripeFragment(1, 0, 0, 20)], fragments);
    }

    [Fact]
    public void SplitAcrossStripes_ChunkStraddlingOneBoundary_IsSplitInTwo()
    {
        // Arrange
        var layout = new StripeLayout(0, 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 90, length: 30);

        // Assert
        Assert.Equal(
            [
                new StripeFragment(0, 90, 0, 10),
                new StripeFragment(1, 0, 10, 20)
            ],
            fragments);
    }

    [Fact]
    public void SplitAcrossStripes_ChunkLargerThanAStripe_IsSplitNWays()
    {
        // Arrange
        var layout = new StripeLayout(0, 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 50, length: 260);

        // Assert
        Assert.Equal(
            [
                new StripeFragment(0, 50, 0, 50),
                new StripeFragment(1, 0, 50, 100),
                new StripeFragment(2, 0, 150, 100),
                new StripeFragment(3, 0, 250, 10)
            ],
            fragments);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 100)]
    [InlineData(0, 512)]
    [InlineData(7, 1)]
    [InlineData(7, 300)]
    [InlineData(1023, 1025)]
    public void SplitAcrossStripes_FragmentsAreContiguousNonEmptyAndSumToTheChunk(long absoluteOffset, int length)
    {
        // Arrange
        var layout = new StripeLayout(0, StripeSize: 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset, length);

        // Assert
        Assert.NotEmpty(fragments);
        Assert.All(fragments, fragment => Assert.True(fragment.Length > 0));
        Assert.Equal(length, fragments.Sum(fragment => fragment.Length));

        var expectedSourceOffset = 0;
        foreach (var fragment in fragments)
        {
            Assert.Equal(expectedSourceOffset, fragment.SourceOffset);
            var absolute = absoluteOffset + fragment.SourceOffset;
            Assert.Equal(layout.StripeIndexOf(absolute), fragment.StripeIndex);
            Assert.Equal(layout.OffsetWithinStripe(absolute), fragment.OffsetInStripe);
            Assert.True(fragment.OffsetInStripe + fragment.Length <= layout.StripeSize);
            expectedSourceOffset += fragment.Length;
        }
    }

    [Fact]
    public void SplitAcrossStripes_ZeroLength_ReturnsNoFragments()
    {
        // Arrange
        var layout = new StripeLayout(0, 100);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 50, length: 0);

        // Assert
        Assert.Empty(fragments);
    }

    [Fact]
    public void SplitAcrossStripes_WithoutAStripeSize_ReturnsASingleFragment()
    {
        // Arrange
        var layout = new StripeLayout(0, StripeSize: 0);

        // Act
        var fragments = layout.SplitAcrossStripes(absoluteOffset: 50, length: 300);

        // Assert
        Assert.Equal([new StripeFragment(0, 50, 0, 300)], fragments);
    }

    [Theory]
    [InlineData(1000, 0, 10, 100)]
    [InlineData(1000, 5000, 10, 100)]
    [InlineData(100_000, 5000, 10, 500)]
    [InlineData(0, 1001, 10, 101)]
    [InlineData(0, 0, 10, 1)]
    public void MinimumChunkSize_IsDerivedFromTheFullestStripe(
        long totalLength, long stripeSize, int maxBlocksPerStripe, long expected)
    {
        // Arrange
        var layout = new StripeLayout(totalLength, stripeSize);

        // Act
        var minimumChunkSize = layout.MinimumChunkSize(maxBlocksPerStripe);

        // Assert
        Assert.Equal(expected, minimumChunkSize);
    }

    [Fact]
    public void ForUpload_SmallPartial_IsBoundedByItsOwnLengthNotTheStripeSize()
    {
        // Arrange
        const long partialLength = 1024 * 1024;

        // Act
        var layout = StripeLayout.ForUpload(partialLength, 256 * GiB, isConcatenationPartial: true);

        // Assert
        Assert.Equal((partialLength + 49_999) / 50_000, layout.MinimumChunkSize(50_000));
    }

    [Fact]
    public void ForUpload_PlainUpload_IsBoundedByTheWholeFileBecauseItIsNotStriped()
    {
        // Arrange
        var uploadLength = 1 * TiB;

        // Act
        var layout = StripeLayout.ForUpload(uploadLength, 256 * GiB, isConcatenationPartial: false);

        // Assert
        Assert.False(layout.IsStriped);
        Assert.Equal((uploadLength + 49_999) / 50_000, layout.MinimumChunkSize(50_000));
    }

    [Fact]
    public void ForUpload_LargePartial_IsBoundedByTheStripeSize()
    {
        // Arrange
        var partialLength = 4 * TiB;

        // Act
        var layout = StripeLayout.ForUpload(partialLength, 256 * GiB, isConcatenationPartial: true);

        // Assert
        Assert.Equal(((256 * GiB) + 49_999) / 50_000, layout.MinimumChunkSize(50_000));
    }

    [Theory]
    [InlineData(50_000, 19)]
    [InlineData(8, 111_111)]
    [InlineData(1, 499_999)]
    public void MaxStripesPerPartial_LeavesRoomForOneStraddledBlockPerStripe(int maxBlocksPerStripe, int expected)
    {
        // Act
        var maxStripesPerPartial = StripeLayout.MaxStripesPerPartial(maxBlocksPerStripe);

        // Assert
        Assert.Equal(expected, maxStripesPerPartial);
    }

    [Fact]
    public void MaxStripesPerPartial_AtTheBoundStaysWithinTheBlockIdSpace()
    {
        // Arrange
        const int maxBlocksPerStripe = 50_000;
        var maxStripesPerPartial = StripeLayout.MaxStripesPerPartial(maxBlocksPerStripe);

        // Act
        var worstCaseBlockIndex = (long)maxStripesPerPartial * (maxBlocksPerStripe + 1);
        var oneStripeFurther = (long)(maxStripesPerPartial + 1) * (maxBlocksPerStripe + 1);

        // Assert
        Assert.True(worstCaseBlockIndex <= StripeLayout.MaxNamespacedBlockIndex);
        Assert.True(oneStripeFurther > StripeLayout.MaxNamespacedBlockIndex);
    }

    [Fact]
    public void MaxPartialLength_ScalesWithTheStripeSize()
    {
        // Act
        var maxPartialLength = StripeLayout.MaxPartialLength(256 * GiB, maxBlocksPerStripe: 50_000);

        // Assert
        Assert.Equal(19 * 256 * GiB, maxPartialLength);
    }

    [Fact]
    public void MaxPartialLength_UnstripedTransferIsUnbounded()
    {
        // Act
        var maxPartialLength = StripeLayout.MaxPartialLength(stripeSize: 0, maxBlocksPerStripe: 50_000);

        // Assert
        Assert.Equal(long.MaxValue, maxPartialLength);
    }

    [Fact]
    public void TwoTebibytesAtTheDefaultStripeSize_FitsTheBlockBudget()
    {
        // Arrange
        var layout = new StripeLayout(2 * TiB, 256 * GiB);

        // Act
        var blocksInFirstStripe = layout.LengthOfStripe(0) / (32 * 1024 * 1024);

        // Assert
        Assert.Equal(8, layout.StripeCount);
        Assert.True(layout.MinimumChunkSize(50_000) <= 32 * 1024 * 1024);
        Assert.Equal(8192, blocksInFirstStripe);
    }

    [Fact]
    public void GetStripeBlobName_StripeZeroKeepsTheHistoricalName()
    {
        // Arrange
        var fileTransferId = Guid.NewGuid();

        // Act
        var stripeZeroName = StripeLayout.GetStripeBlobName(fileTransferId, 0);
        var stripeOneName = StripeLayout.GetStripeBlobName(fileTransferId, 1);
        var stripeFortyTwoName = StripeLayout.GetStripeBlobName(fileTransferId, 42);

        // Assert
        Assert.Equal(fileTransferId.ToString(), stripeZeroName);
        Assert.Equal($"{fileTransferId}/stripe-0001", stripeOneName);
        Assert.Equal($"{fileTransferId}/stripe-0042", stripeFortyTwoName);
        Assert.StartsWith(StripeLayout.GetStripeBlobPrefix(fileTransferId), stripeOneName);
    }

    [Fact]
    public void GetStripeBlobName_RejectsAStripeIndexThatWouldWidenTheName()
    {
        // Arrange
        var fileTransferId = Guid.NewGuid();

        // Act
        var widenTheName = () => StripeLayout.GetStripeBlobName(fileTransferId, 10_000);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(widenTheName);
    }
}
