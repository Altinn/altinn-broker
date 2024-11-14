using System.Security.Cryptography;

namespace Altinn.Broker.Tests.LargeFile;
/**
 * Pseudo-random generator
*/
public class PseudoRandomDataStream : Stream
{
    private long _position;
    private long _length;
    private XorShiftRandom _rng;
    private Timer _timer;
    private int timerElapsedCount = 0;

    long bytesRead = 0;

    public PseudoRandomDataStream(long length)
    {
        _length = length;
        _position = 0;
        _rng = new XorShiftRandom();
        _timer = new Timer(OnTimerElapsed, null, 1000, 1000);
    }

    private void OnTimerElapsed(object state)
    {
        timerElapsedCount++;
        Console.WriteLine($"Current position {(_position * 100.0 / _length):F3}%. Average speed so far is {(_position / timerElapsedCount) / (1024 * 1024)} MiB/s");
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override bool CanTimeout => base.CanTimeout;
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) {
            Console.WriteLine("Stream cancellation requested");
            return Task.FromResult(0);
        }
        return Task.FromResult(Read(buffer, offset, count));
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
        {
            return 0;
        }
        long bytesLeftToRead = _length - _position;
        int bytesToRead = (int)Math.Min(count, Math.Min(bytesLeftToRead, int.MaxValue));
        if (bytesToRead <= 0) return 0;
        _rng.NextBytes(buffer, 0, bytesToRead);
        _position += bytesToRead;
        return bytesToRead;
    }

    public override void Flush() {
        Console.WriteLine("Flush called");        
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Console.WriteLine($"Seek called with offset {offset} from origin {origin}");
        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;
                break;
            case SeekOrigin.Current:
                Position += offset;
                break;
            case SeekOrigin.End:
                Position = _length + offset;
                break;
            default:
                throw new ArgumentException("Invalid seek origin");
        }
        return Position;
    }

    public override void SetLength(long value)
    {
        Console.WriteLine($"Length set to {value}");
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Length cannot be negative.");
        _length = value;
        if (_position > _length)
            _position = _length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("This stream does not support writing.");
    }

    protected override void Dispose(bool disposing)
    {
        _timer.Dispose();
        base.Dispose(disposing);
    }
}

public class XorShiftRandom
{
    private ulong _state;

    public XorShiftRandom()
    {
        _state = (ulong)DateTime.UtcNow.Ticks;
    }

    public void NextBytes(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (i % 8 == 0)
            {
                ulong x = _state;
                x ^= x << 13;
                x ^= x >> 7;
                x ^= x << 17;
                _state = x;
            }
            buffer[offset + i] = (byte)(_state >> ((i % 8) * 8));
        }
    }
}
