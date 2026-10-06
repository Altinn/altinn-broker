using System.Buffers.Text;
using System.Text;

namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Where a list continues from: the sort position of the last file transfer on the previous page.
/// </summary>
/// <remarks>
/// Both parts are needed. The timestamp alone is not unique, so a page boundary that falls between
/// two file transfers sharing a timestamp would either skip or repeat one.
///
/// The token is opaque but not signed: a caller who alters it only moves the window within the file
/// transfers they are already authorized to see, since authorization is applied independently of it.
/// </remarks>
public readonly record struct FileTransferListCursor(DateTimeOffset SortDate, Guid FileTransferId)
{
    public string ToToken()
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{SortDate.UtcTicks}:{FileTransferId:N}"));

    public static FileTransferListCursor? FromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token)).Split(':');
            if (parts.Length != 2
                || !long.TryParse(parts[0], out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out var fileTransferId))
            {
                return null;
            }

            return new FileTransferListCursor(new DateTimeOffset(ticks, TimeSpan.Zero), fileTransferId);
        }
        catch (FormatException)
        {
            // A token that is not even base64 is a malformed request, not a reason to fail the call.
            return null;
        }
    }
}
