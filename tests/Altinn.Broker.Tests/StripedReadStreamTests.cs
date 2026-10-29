using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Altinn.Broker.Tests;

public class StripedReadStreamTests
{
    private sealed class FakeStripeSource(byte[] source, long stripeSize)
    {
        public List<int> OpenedStripes { get; } = [];
        public List<TrackingStream> OpenedStreams { get; } = [];
        public int? TruncateStripe { get; set; }

        public StripeOpener Opener => (stripeIndex, offsetWithinStripe, length, _) =>
        {
            OpenedStripes.Add(stripeIndex);
            var start = (stripeIndex * stripeSize) + offsetWithinStripe;
            var available = TruncateStripe == stripeIndex ? Math.Max(length - 1, 0) : length;
            var stream = new TrackingStream(source.AsSpan((int)start, (int)available).ToArray());
            OpenedStreams.Add(stream);
            return ValueTask.FromResult<Stream>(stream);
        };
    }

    private sealed class TrackingStream(byte[] buffer) : MemoryStream(buffer, writable: false)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return base.DisposeAsync();
        }
    }

    private static byte[] CreateSource(int length)
    {
        var source = new byte[length];
        for (var i = 0; i < length; i++)
        {
            source[i] = (byte)(i % 251);
        }

        return source;
    }

    private static StripedReadStream Create(byte[] source, long stripeSize, ByteRange range, out FakeStripeSource fake)
    {
        fake = new FakeStripeSource(source, stripeSize);
        return new StripedReadStream(
            new StripeLayout(source.Length, stripeSize),
            range,
            fake.Opener,
            NullLogger.Instance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(42)]
    public async Task ReadToEnd_ReproducesTheSource(long stripeSize)
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize, new ByteRange(0, source.Length), out _);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination);

        // Assert
        Assert.Equal(source, destination.ToArray());
    }

    [Fact]
    public async Task ReadAsync_WithASingleByteBuffer_NeverReturnsZeroBeforeTheEnd()
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(0, source.Length), out _);
        var buffer = new byte[1];

        // Act
        var readCounts = new List<int>();
        for (var i = 0; i < source.Length; i++)
        {
            readCounts.Add(await stream.ReadAsync(buffer));
            Assert.Equal(source[i], buffer[0]);
        }

        var readAfterEnd = await stream.ReadAsync(buffer);

        // Assert
        Assert.All(readCounts, read => Assert.Equal(1, read));
        Assert.Equal(0, readAfterEnd);
    }

    [Fact]
    public async Task ReadAsync_ABufferStraddlingABoundary_ReadsShortThenContinues()
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(0, source.Length), out _);
        var buffer = new byte[20];

        // Act
        var first = await stream.ReadAsync(buffer);
        var second = await stream.ReadAsync(buffer);
        var third = await stream.ReadAsync(buffer);
        var fourth = await stream.ReadAsync(buffer);

        // Assert
        Assert.Equal(16, first);
        Assert.Equal(16, second);
        Assert.Equal(9, third);
        Assert.Equal(0, fourth);
    }

    [Theory]
    [InlineData(0, 41)]
    [InlineData(0, 10)]
    [InlineData(10, 20)]
    [InlineData(10, 31)]
    [InlineData(16, 16)]
    [InlineData(31, 10)]
    [InlineData(40, 1)]
    public async Task Range_DeliversExactlyTheRequestedBytes(int offset, int length)
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(offset, length), out _);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination);

        // Assert
        Assert.Equal(length, destination.Length);
        Assert.Equal(source.AsSpan(offset, length).ToArray(), destination.ToArray());
    }

    [Fact]
    public async Task Range_SpanningThreeStripes_DeliversExactlyTheRequestedBytes()
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(10, 25), out var fake);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination);

        // Assert
        Assert.Equal(source.AsSpan(10, 25).ToArray(), destination.ToArray());
        Assert.Equal([0, 1, 2], fake.OpenedStripes);
    }

    [Fact]
    public async Task Range_InsideOneStripe_OpensOnlyThatStripe()
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(20, 5), out var fake);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination);

        // Assert
        Assert.Equal([1], fake.OpenedStripes);
    }

    [Fact]
    public async Task TruncatedStripe_Throws()
    {
        // Arrange
        var source = CreateSource(41);
        var stream = Create(source, stripeSize: 16, new ByteRange(0, source.Length), out var fake);
        fake.TruncateStripe = 1;
        using var destination = new MemoryStream();

        // Act
        var copyToEnd = () => stream.CopyToAsync(destination);

        // Assert
        await using (stream)
        {
            await Assert.ThrowsAsync<EndOfStreamException>(copyToEnd);
        }
    }

    [Fact]
    public async Task ExhaustedStripesAreDisposedAsTheReadMovesOn()
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(0, source.Length), out var fake);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination);

        // Assert
        Assert.Equal(3, fake.OpenedStreams.Count);
        Assert.All(fake.OpenedStreams, opened => Assert.True(opened.WasDisposed));
    }

    [Fact]
    public async Task DisposingMidStream_DisposesTheOpenStripe()
    {
        // Arrange
        var source = CreateSource(41);
        var stream = Create(source, stripeSize: 16, new ByteRange(0, source.Length), out var fake);
        _ = await stream.ReadAsync(new byte[4]);

        // Act
        await stream.DisposeAsync();

        // Assert
        Assert.Single(fake.OpenedStreams);
        Assert.True(fake.OpenedStreams[0].WasDisposed);
    }

    [Fact]
    public async Task PrimeAsync_OpensTheFirstStripeUpFront()
    {
        // Arrange
        var source = CreateSource(41);
        await using var stream = Create(source, stripeSize: 16, new ByteRange(20, 5), out var fake);
        Assert.Empty(fake.OpenedStripes);

        // Act
        await stream.PrimeAsync(CancellationToken.None);

        // Assert
        Assert.Equal([1], fake.OpenedStripes);
    }

    [Fact]
    public void IsNotSeekable()
    {
        // Arrange
        var source = CreateSource(41);
        using var stream = Create(source, stripeSize: 16, new ByteRange(0, source.Length), out _);

        // Act
        var canSeek = stream.CanSeek;

        // Assert
        Assert.False(canSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position = 5);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }
}
