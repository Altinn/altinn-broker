namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// Forward-only stream wrapper that counts bytes read.
/// Optionally exposes a known content length for callers (e.g. Azure Put Block) without seeking.
/// Does not take ownership of the inner stream.
/// </summary>
internal sealed class ByteCountingStream(Stream inner, long? knownLength = null) : Stream
{
    public long BytesRead { get; private set; }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => knownLength ?? throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        if (read > 0)
        {
            BytesRead += read;
        }

        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, offset, count, cancellationToken);
        if (read > 0)
        {
            BytesRead += read;
        }

        return read;
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
        => ReadAsyncMemoryCore(buffer, cancellationToken);

    private async ValueTask<int> ReadAsyncMemoryCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        if (read > 0)
        {
            BytesRead += read;
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
