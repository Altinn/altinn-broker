using Altinn.Broker.Core.Domain;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Core.Helpers;

/// <summary>
/// Opens a stripe blob and returns a stream positioned at <paramref name="offsetWithinStripe"/> that
/// yields exactly <paramref name="length"/> bytes.
/// </summary>
public delegate ValueTask<Stream> StripeOpener(
    int stripeIndex,
    long offsetWithinStripe,
    long length,
    CancellationToken cancellationToken);

/// <summary>
/// Reads a byte range of a striped file transfer as one continuous stream, opening each stripe blob
/// only when the read reaches it. A full download is the maximal range.
/// Not seekable: FileStreamResult would otherwise overwrite the Content-Length the controller sets by
/// hand, breaking every partial content response.
/// </summary>
public sealed class StripedReadStream : Stream
{
    private readonly StripeLayout _layout;
    private readonly ByteRange _range;
    private readonly StripeOpener _openStripe;
    private readonly ILogger _logger;

    private Stream? _currentStripe;
    private int _currentStripeIndex = -1;
    private long _currentStripeRemaining;
    private long _position;

    public StripedReadStream(StripeLayout layout, ByteRange range, StripeOpener openStripe, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(openStripe);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegative(range.Offset);
        ArgumentOutOfRangeException.ThrowIfNegative(range.Length);

        _layout = layout;
        _range = range;
        _openStripe = openStripe;
        _logger = logger;
    }

    /// <summary>
    /// Opens the first stripe up front, so a missing blob surfaces before any response headers are
    /// written rather than as a mid-body connection reset.
    /// </summary>
    public async Task PrimeAsync(CancellationToken cancellationToken)
    {
        if (_currentStripe is null && _range.Length > 0)
        {
            await OpenNextStripeAsync(cancellationToken);
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        // The only place end of stream is reported.
        var remaining = _range.Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        if (_currentStripe is null)
        {
            await OpenNextStripeAsync(cancellationToken);
        }

        // The int bound goes last so the long comparisons cannot overflow.
        var maxRead = (int)Math.Min(Math.Min((long)buffer.Length, _currentStripeRemaining), remaining);
        var read = await _currentStripe!.ReadAsync(buffer[..maxRead], cancellationToken);
        if (read == 0)
        {
            throw new EndOfStreamException(
                $"Stripe {_currentStripeIndex} ended {_currentStripeRemaining} bytes early. " +
                "The stored content is shorter than the recorded stripe layout.");
        }

        _position += read;
        _currentStripeRemaining -= read;
        if (_currentStripeRemaining == 0)
        {
            await DisposeCurrentStripeAsync();
        }

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    private async Task OpenNextStripeAsync(CancellationToken cancellationToken)
    {
        var absoluteOffset = _range.Offset + _position;
        _currentStripeIndex = _layout.StripeIndexOf(absoluteOffset);
        var offsetWithinStripe = _layout.OffsetWithinStripe(absoluteOffset);
        var length = Math.Min(
            _layout.LengthOfStripe(_currentStripeIndex) - offsetWithinStripe,
            _range.Length - _position);

        if (length <= 0)
        {
            throw new EndOfStreamException(
                $"Stripe {_currentStripeIndex} has no bytes left to serve at offset {absoluteOffset}. " +
                "The recorded stripe layout does not match the requested range.");
        }

        _logger.LogDebug(
            "Opening stripe {StripeIndex} at offset {OffsetWithinStripe} for {Length} bytes",
            _currentStripeIndex,
            offsetWithinStripe,
            length);

        _currentStripe = await _openStripe(_currentStripeIndex, offsetWithinStripe, length, cancellationToken);
        _currentStripeRemaining = length;
    }

    private async ValueTask DisposeCurrentStripeAsync()
    {
        if (_currentStripe is not null)
        {
            await _currentStripe.DisposeAsync();
            _currentStripe = null;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await DisposeCurrentStripeAsync();
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentStripe?.Dispose();
            _currentStripe = null;
        }

        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
