namespace Altinn.Broker.LargeFileLoadTester;
public class XorShiftDataStream : Stream
{
    private long _position;
    private long _length;
    private byte[] _buffer;
    private readonly XorShiftRandom _rng;

    long bytesRead = 0;
    long milestoneSize => _length / 100;
    long lastMileStoneRead = 0;
    int milestoneCount = 0;

    int bufferCount = 0;

    public XorShiftDataStream(long length, int bufferSize)
    {
        _length = length;
        _position = 0;
        _buffer = new byte[bufferSize];
        _rng = new XorShiftRandom();
        Console.WriteLine("Length: " + _length.ToString("N0"));
        Console.WriteLine("Milestone size: " + milestoneSize.ToString("N0"));
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
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
        return Task.FromResult(Read(buffer, offset, count));
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        bufferCount++;
        bytesRead += count;
        if ((bytesRead - lastMileStoneRead) >= (milestoneSize * (milestoneCount+1)))
        {
            lastMileStoneRead = bytesRead;
            milestoneCount++;
            Console.WriteLine($"Read {bytesRead.ToString("N0")} bytes in buffer {bufferCount.ToString("N0")}, reached milestone {milestoneCount}.");
        }

        if (_position >= _length)
            return 0;

        long bytesLeftToRead = _length - _position;
        int bytesToRead = (int)Math.Min(count, Math.Min(bytesLeftToRead, int.MaxValue));
        if (bytesToRead <= 0) return 0;

        if (bytesToRead > _buffer.Length)
        {
            _rng.NextBytes(buffer, offset, bytesToRead);
        }
        else
        {
            _rng.NextBytes(_buffer, 0, bytesToRead);
            Buffer.BlockCopy(_buffer, 0, buffer, offset, bytesToRead);
        }

        _position += bytesToRead;
        return bytesToRead;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
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
