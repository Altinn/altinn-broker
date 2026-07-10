using Altinn.Broker.API.Helpers;
using Altinn.Broker.Core.Domain;

using Xunit;

namespace Altinn.Broker.Tests;

public class ByteRangeTests
{
    [Theory]
    // bytes=start-end
    [InlineData(0, 9, 100, 0, 10)]
    [InlineData(50, 99, 100, 50, 50)]
    [InlineData(0, 0, 100, 0, 1)]
    [InlineData(99, 99, 100, 99, 1)]
    // end beyond file end is clamped per RFC 9110
    [InlineData(50, 9999, 100, 50, 50)]
    public void Resolve_StartEndRange_ReturnsExpectedRange(long start, long end, long totalLength, long expectedOffset, long expectedLength)
    {
        var resolved = new ByteRangeRequest(start, end).Resolve(totalLength);
        Assert.Equal(new ByteRange(expectedOffset, expectedLength), resolved);
    }

    [Theory]
    [InlineData(0, 100, 0, 100)]
    [InlineData(60, 100, 60, 40)]
    [InlineData(99, 100, 99, 1)]
    public void Resolve_OpenEndedRange_ReturnsRestOfFile(long start, long totalLength, long expectedOffset, long expectedLength)
    {
        var resolved = new ByteRangeRequest(start, null).Resolve(totalLength);
        Assert.Equal(new ByteRange(expectedOffset, expectedLength), resolved);
    }

    [Theory]
    [InlineData(10, 100, 90, 10)]
    // suffix longer than the file returns the whole file
    [InlineData(9999, 100, 0, 100)]
    public void Resolve_SuffixRange_ReturnsLastBytes(long suffixLength, long totalLength, long expectedOffset, long expectedLength)
    {
        var resolved = new ByteRangeRequest(null, suffixLength).Resolve(totalLength);
        Assert.Equal(new ByteRange(expectedOffset, expectedLength), resolved);
    }

    [Theory]
    // start at or beyond file end
    [InlineData(100L, null, 100)]
    [InlineData(150L, 200L, 100)]
    // zero-length suffix
    [InlineData(null, 0L, 100)]
    // end before start
    [InlineData(50L, 40L, 100)]
    // any range on an empty file
    [InlineData(0L, 9L, 0)]
    [InlineData(null, 10L, 0)]
    public void Resolve_UnsatisfiableRange_ReturnsNull(long? start, long? end, long totalLength)
    {
        Assert.Null(new ByteRangeRequest(start, end).Resolve(totalLength));
    }

    [Theory]
    [InlineData("bytes=0-9", 0L, 9L)]
    [InlineData("bytes=100-", 100L, null)]
    [InlineData("bytes=-10", null, 10L)]
    [InlineData("BYTES=0-9", 0L, 9L)]
    public void ParseRangeHeader_SingleBytesRange_ReturnsRequestedRange(string header, long? expectedStart, long? expectedEnd)
    {
        var parsed = ByteRangeParser.ParseRangeHeader(header);
        Assert.Equal(new ByteRangeRequest(expectedStart, expectedEnd), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bytes=")]
    [InlineData("bytes=abc")]
    [InlineData("bytes=9-2")]
    [InlineData("items=0-9")]
    [InlineData("bytes=0-1,5-10")]
    public void ParseRangeHeader_UnsupportedOrMalformed_ReturnsNull(string? header)
    {
        Assert.Null(ByteRangeParser.ParseRangeHeader(header));
    }
}
