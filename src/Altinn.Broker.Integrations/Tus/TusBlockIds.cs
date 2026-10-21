using System.Text;

namespace Altinn.Broker.Integrations.Tus;

internal static class TusBlockIds
{
    public const int MaxBlocksPerBlob = 50_000;

    public static string BuildSequentialBlockId(long blockIndex)
    {
        var blockName = blockIndex.ToString("D12");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(blockName));
    }

    public static string BuildNamespacedBlockId(int partialIndex, long blockIndex)
    {
        var blockName = $"{partialIndex:D6}{blockIndex:D6}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(blockName));
    }

    public static long? TryParseSortableIndex(string blockId)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(blockId));
            if (long.TryParse(decoded, out var index))
            {
                return index;
            }

            if (decoded.Length == 12
                && int.TryParse(decoded[..6], out var partialIndex)
                && int.TryParse(decoded[6..], out var chunkIndex))
            {
                return (partialIndex * 1_000_000L) + chunkIndex;
            }
        }
        catch (FormatException)
        {
            return null;
        }

        return null;
    }

    public static bool TryParseNamespacedBlockId(string blockId, out int partialIndex, out int chunkIndex)
    {
        partialIndex = 0;
        chunkIndex = 0;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(blockId));
            if (decoded.Length == 12
                && int.TryParse(decoded[..6], out partialIndex)
                && int.TryParse(decoded[6..], out chunkIndex))
            {
                return true;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        partialIndex = 0;
        chunkIndex = 0;
        return false;
    }

    /// <summary>
    /// Block ids to commit, in Upload-Concat order. A repeated partial is emitted again, so
    /// <see cref="TotalLength"/> counts that partial's staged bytes once per occurrence.
    /// </summary>
    public sealed record ConcatCommitList(IReadOnlyList<string> BlockIds, long TotalLength);

    /// <summary>
    /// Appends each partial's staged blocks, in chunk order, once per Upload-Concat occurrence.
    /// <paramref name="partialIndexesInOrder"/> is the creation-order index of each listed partial.
    /// </summary>
    public static ConcatCommitList ExpandByConcatOrder(
        IReadOnlyList<string> blockIds,
        IReadOnlyList<long> blockLengths,
        IReadOnlyList<int> partialIndexesInOrder)
    {
        if (blockIds.Count != blockLengths.Count)
        {
            throw new InvalidOperationException("Staged block ids and lengths do not match.");
        }

        var blocksByPartial = new Dictionary<int, List<(int ChunkIndex, string BlockId, long Length)>>();
        for (var i = 0; i < blockIds.Count; i++)
        {
            if (!TryParseNamespacedBlockId(blockIds[i], out var partialIndex, out var chunkIndex))
            {
                throw new InvalidOperationException(
                    $"Staged block '{blockIds[i]}' is not a concatenation partial block.");
            }

            if (!blocksByPartial.TryGetValue(partialIndex, out var blocks))
            {
                blocks = [];
                blocksByPartial[partialIndex] = blocks;
            }

            blocks.Add((chunkIndex, blockIds[i], blockLengths[i]));
        }

        foreach (var blocks in blocksByPartial.Values)
        {
            blocks.Sort(static (left, right) => left.ChunkIndex.CompareTo(right.ChunkIndex));
        }

        var listedPartialIndexes = new HashSet<int>(partialIndexesInOrder);
        foreach (var partialIndex in blocksByPartial.Keys)
        {
            if (!listedPartialIndexes.Contains(partialIndex))
            {
                throw new InvalidOperationException(
                    $"Staged block for partial index {partialIndex} is not part of the Upload-Concat list.");
            }
        }

        var expandedBlockIds = new List<string>();
        var totalLength = 0L;
        foreach (var partialIndex in partialIndexesInOrder)
        {
            if (!blocksByPartial.TryGetValue(partialIndex, out var blocks))
            {
                continue;
            }

            foreach (var block in blocks)
            {
                expandedBlockIds.Add(block.BlockId);
                totalLength += block.Length;
            }
        }

        return new ConcatCommitList(expandedBlockIds, totalLength);
    }

    public static long GetMinimumChunkSizeBytes(long uploadLength)
        => uploadLength <= 0 ? 1 : (uploadLength + MaxBlocksPerBlob - 1) / MaxBlocksPerBlob;
}
