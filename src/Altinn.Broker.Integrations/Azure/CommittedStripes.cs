using Altinn.Broker.Core.Domain;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Altinn.Broker.Integrations.Azure;

/// <summary>
/// The stripe blobs that exist for a file transfer, read from storage.
/// Listing returns committed blobs only, so staged blocks that were never committed are not counted.
/// </summary>
public sealed record CommittedStripes(int StripeCount, long TotalLength, IReadOnlyList<long> StripeLengths)
{
    /// <summary>
    /// Every stripe but the last is exactly the stripe size, which the concatenation chain asserts before
    /// committing, so stripe 0's length is the stripe size whenever the content spans more than one blob.
    /// </summary>
    public long? StripeSizeBytes => StripeCount > 1 ? StripeLengths[0] : null;

    public static async Task<CommittedStripes> ReadAsync(
        BlobContainerClient containerClient,
        Guid fileTransferId,
        CancellationToken cancellationToken)
    {
        var lengthsByStripe = new SortedDictionary<int, long>();
        await foreach (var blob in containerClient.GetBlobsAsync(
            BlobTraits.None,
            BlobStates.None,
            StripeLayout.GetStripeBlobPrefix(fileTransferId),
            cancellationToken))
        {
            if (StripeLayout.TryParseStripeIndex(blob.Name, fileTransferId, out var stripeIndex))
            {
                lengthsByStripe[stripeIndex] = blob.Properties.ContentLength ?? 0;
            }
        }

        var stripeLengths = lengthsByStripe.Values.ToList();
        return new CommittedStripes(stripeLengths.Count, stripeLengths.Sum(), stripeLengths);
    }
}
