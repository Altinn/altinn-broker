namespace Altinn.Broker.Core.Domain;

/// <summary>
/// A contiguous piece of an upload chunk belonging to one stripe blob.
/// </summary>
public readonly record struct StripeFragment(int StripeIndex, long OffsetInStripe, int SourceOffset, int Length);

/// <summary>
/// How a file transfer's bytes are laid out across one or more Azure block blobs. Byte A lives at
/// offset <c>A % StripeSize</c> in stripe <c>A / StripeSize</c>, so writer and reader derive the
/// mapping independently with no manifest to keep in sync.
/// A <see cref="StripeSize"/> of zero means a single blob named <c>{fileTransferId}</c>.
/// </summary>
public readonly record struct StripeLayout(long TotalLength, long StripeSize)
{
    /// <summary>Stripe indices are formatted as D4 in <see cref="GetStripeBlobName"/>.</summary>
    public const int MaxStripeIndex = 9999;

    /// <summary>
    /// Picks the single-blob fast path on reads. Writes key off <see cref="StripeSize"/> alone, since a
    /// concatenated upload's total length is unknown until the final POST.
    /// </summary>
    public bool IsStriped => StripeSize > 0 && TotalLength > StripeSize;

    public int StripeCount => !IsStriped ? 1 : (int)((TotalLength + StripeSize - 1) / StripeSize);

    public int StripeIndexOf(long absoluteOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(absoluteOffset);
        return StripeSize <= 0 ? 0 : (int)(absoluteOffset / StripeSize);
    }

    public long OffsetWithinStripe(long absoluteOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(absoluteOffset);
        return StripeSize <= 0 ? absoluteOffset : absoluteOffset % StripeSize;
    }

    /// <summary>
    /// Length of a single stripe. Every stripe but the last is exactly <see cref="StripeSize"/> bytes;
    /// the last one holds the remainder.
    /// </summary>
    public long LengthOfStripe(int stripeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stripeIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(stripeIndex, StripeCount);
        return StripeSize <= 0 ? TotalLength : Math.Min(StripeSize, TotalLength - ((long)stripeIndex * StripeSize));
    }

    /// <summary>
    /// One fragment per stripe the chunk touches. Chunk sizes are the client's choice and vary between
    /// requests, so a chunk can straddle any number of boundaries.
    /// </summary>
    public IReadOnlyList<StripeFragment> SplitAcrossStripes(long absoluteOffset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(absoluteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length == 0)
        {
            return [];
        }
        if (StripeSize <= 0)
        {
            return [new StripeFragment(0, absoluteOffset, 0, length)];
        }

        var fragments = new List<StripeFragment>(1);
        var sourceOffset = 0;
        while (sourceOffset < length)
        {
            var offset = absoluteOffset + sourceOffset;
            var offsetWithinStripe = offset % StripeSize;
            // Cannot overflow int: bounded by the remaining chunk length.
            var fragmentLength = (int)Math.Min(StripeSize - offsetWithinStripe, length - sourceOffset);
            fragments.Add(new StripeFragment((int)(offset / StripeSize), offsetWithinStripe, sourceOffset, fragmentLength));
            sourceOffset += fragmentLength;
        }

        return fragments;
    }

    /// <summary>
    /// The smallest chunk size that will not exhaust a stripe's block budget. Only the fullest stripe
    /// matters.
    /// </summary>
    public long MinimumChunkSize(int maxBlocksPerStripe)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBlocksPerStripe);

        // Length 0 means "not known yet", so assume a full stripe and err on the strict side.
        var fullestStripe = StripeSize switch
        {
            <= 0 => TotalLength,
            _ when TotalLength <= 0 => StripeSize,
            _ => Math.Min(TotalLength, StripeSize)
        };

        return fullestStripe <= 0 ? 1 : (fullestStripe + maxBlocksPerStripe - 1) / maxBlocksPerStripe;
    }

    public static StripeLayout For(FileTransferEntity fileTransfer) =>
        new(fileTransfer.FileTransferSize, fileTransfer.StripeSizeBytes ?? 0);

    /// <summary>
    /// Stripe 0 keeps the historical blob name, so an unstriped transfer is stored where it always was.
    /// </summary>
    public static string GetStripeBlobName(Guid fileTransferId, int stripeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stripeIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stripeIndex, MaxStripeIndex);
        return stripeIndex == 0 ? fileTransferId.ToString() : $"{fileTransferId}/stripe-{stripeIndex:D4}";
    }

    /// <summary>Matches stripe 0 and every later stripe, for listing and deletion.</summary>
    public static string GetStripeBlobPrefix(Guid fileTransferId) => fileTransferId.ToString();

    /// <summary>
    /// The inverse of <see cref="GetStripeBlobName"/>. Returns false when the blob is not a stripe of this transfer.
    /// </summary>
    public static bool TryParseStripeIndex(string blobName, Guid fileTransferId, out int stripeIndex)
    {
        stripeIndex = 0;
        var id = fileTransferId.ToString();
        if (string.Equals(blobName, id, StringComparison.Ordinal))
        {
            return true;
        }

        var stripePrefix = $"{id}/stripe-";
        return blobName.StartsWith(stripePrefix, StringComparison.Ordinal)
            && int.TryParse(blobName.AsSpan(stripePrefix.Length), out stripeIndex);
    }

    /// <summary>
    /// Derives a transfer's stripe size from its resource's maximum. Call once, at initialize: changing
    /// it later would change how already-written content is read.
    /// </summary>
    public static long DeriveStripeSizeBytes(long maxTransferSize, int maxStripes, long minStripeBytes, long maxStripeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStripes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minStripeBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStripeBytes, minStripeBytes);

        if (maxTransferSize <= 0)
        {
            return minStripeBytes;
        }

        return Math.Clamp((maxTransferSize + maxStripes - 1) / maxStripes, minStripeBytes, maxStripeBytes);
    }
}
