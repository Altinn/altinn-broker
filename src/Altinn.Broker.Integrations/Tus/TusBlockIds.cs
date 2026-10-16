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
    /// Orders staged partial blocks by their place in Upload-Concat, then by chunk inside that partial.
    /// <paramref name="concatPositionByPartialIndex"/> maps the creation-order partial index to that place.
    /// </summary>
    public static IReadOnlyList<string> OrderByConcatPosition(
        IReadOnlyList<string> blockIds,
        IReadOnlyDictionary<int, int> concatPositionByPartialIndex)
    {
        var tagged = new List<(string BlockId, int Position, int ChunkIndex)>(blockIds.Count);
        foreach (var blockId in blockIds)
        {
            if (!TryParseNamespacedBlockId(blockId, out var partialIndex, out var chunkIndex))
            {
                throw new InvalidOperationException(
                    $"Staged block '{blockId}' is not a concatenation partial block.");
            }

            if (!concatPositionByPartialIndex.TryGetValue(partialIndex, out var position))
            {
                throw new InvalidOperationException(
                    $"Staged block for partial index {partialIndex} is not part of the Upload-Concat list.");
            }

            tagged.Add((blockId, position, chunkIndex));
        }

        return tagged
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.ChunkIndex)
            .Select(entry => entry.BlockId)
            .ToList();
    }

    public static long GetMinimumChunkSizeBytes(long uploadLength)
        => uploadLength <= 0 ? 1 : (uploadLength + MaxBlocksPerBlob - 1) / MaxBlocksPerBlob;
}
