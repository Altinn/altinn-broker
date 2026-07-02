using System.Security.Cryptography;

namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// Passes reads through to the inner stream while updating an incremental MD5 hash.
/// Does not take ownership of the inner stream.
/// </summary>
internal sealed class Md5ComputingStream(Stream inner, MD5 md5) : Stream
{
    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

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
            md5.TransformBlock(buffer, offset, read, null, 0);
        }

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, offset, count, cancellationToken);
        if (read > 0)
        {
            md5.TransformBlock(buffer, offset, read, null, 0);
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
