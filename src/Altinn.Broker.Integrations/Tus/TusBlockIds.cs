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

    public static long GetMinimumChunkSizeBytes(long uploadLength)
        => uploadLength <= 0 ? 1 : (uploadLength + MaxBlocksPerBlob - 1) / MaxBlocksPerBlob;
}
