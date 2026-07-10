namespace Altinn.Broker.Core.Domain;

/// <summary>
/// A concrete byte range within a file, resolved against the file's actual size.
/// </summary>
public readonly record struct ByteRange(long Offset, long Length)
{
    public long Start => Offset;
    public long End => Offset + Length - 1;
}

/// <summary>
/// A byte range as requested by a client per RFC 9110, before it has been resolved against the file size.
/// Start = null means a suffix range of the last <see cref="End"/> bytes. End = null means from <see cref="Start"/> to end of file.
/// </summary>
public readonly record struct ByteRangeRequest(long? Start, long? End)
{
    /// <summary>
    /// Resolves the requested range against the total file length per RFC 9110 section 14.1.2.
    /// The end position is clamped to the last byte of the file.
    /// </summary>
    /// <returns>The resolved range, or null if the range is not satisfiable (should result in HTTP 416).</returns>
    public ByteRange? Resolve(long totalLength)
    {
        if (totalLength <= 0)
        {
            return null;
        }
        if (Start is null) // Suffix range: the last {End} bytes
        {
            if (End is null || End.Value <= 0)
            {
                return null;
            }
            var suffixLength = Math.Min(End.Value, totalLength);
            return new ByteRange(totalLength - suffixLength, suffixLength);
        }
        if (Start.Value >= totalLength || (End is not null && End.Value < Start.Value))
        {
            return null;
        }
        var end = End is null ? totalLength - 1 : Math.Min(End.Value, totalLength - 1);
        return new ByteRange(Start.Value, end - Start.Value + 1);
    }
}
