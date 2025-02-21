using Altinn.Broker.Models;

namespace Altinn.Broker.Core.Models;
/// <summary>
/// Overview of a broker file transfer which also includes the status history of the file transfer.|
/// </summary>
public class FileTransferStatusDetailsExt : FileTransferOverviewExt
{
    /// <summary>
    /// The status history of the file transfer.
    /// </summary>
    public List<FileTransferStatusEventExt> FileTransferStatusHistory { get; set; } = new List<FileTransferStatusEventExt>();

    /// <summary>
    /// The status history of the file transfer for each recipient.
    /// </summary>
    public List<RecipientFileTransferStatusEventExt> RecipientFileTransferStatusHistory { get; set; } = new List<RecipientFileTransferStatusEventExt>();
}
