using Altinn.Broker.Core.Domain;

using Microsoft.Net.Http.Headers;

namespace Altinn.Broker.API.Helpers;

public static class ByteRangeParser
{
    /// <summary>
    /// Parses a Range header into a single requested byte range.
    /// Returns null when the header is absent, malformed, uses another unit than bytes, or requests
    /// multiple ranges — per RFC 9110 such headers are ignored and the full file is served.
    /// </summary>
    public static ByteRangeRequest? ParseRangeHeader(string? rangeHeader)
    {
        if (string.IsNullOrWhiteSpace(rangeHeader) || !RangeHeaderValue.TryParse(rangeHeader, out var range))
        {
            return null;
        }
        if (!range.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase) || range.Ranges.Count != 1)
        {
            return null;
        }
        var item = range.Ranges.Single();
        if (item.From is null && item.To is null)
        {
            return null;
        }
        return new ByteRangeRequest(item.From, item.To);
    }
}
