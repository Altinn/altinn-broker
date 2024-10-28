namespace Altinn.Broker.Tests.Helpers;
public class FakeFileStream : Stream
{
    private readonly long _length;
    private long _position;

    public FakeFileStream(long length)
    {
        _length = length;
        _position = 0;
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

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remainingBytes = _length - _position;
        if (remainingBytes <= 0) return 0;

        var bytesToRead = (int)Math.Min(count, remainingBytes);
        // Fill buffer with dummy data
        for (int i = offset; i < offset + bytesToRead; i++)
        {
            buffer[i] = (byte)(i % 256);
        }

        _position += bytesToRead;
        return bytesToRead;
    }

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
                Position = Length + offset;
                break;
        }
        return Position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotImplementedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
}
