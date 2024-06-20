using Altinn.Broker.Models;

namespace Altinn.Broker.Core.Models;

public class FileTransferStatusDetailsExt : FileTransferOverviewExt
{
    public List<FileTransferStatusEventExt> FileTransferStatusHistory { get; set; } = new List<FileTransferStatusEventExt>();
    public List<RecipientFileTransferStatusEventExt> RecipientFileTransferStatusHistory { get; set; } = new List<RecipientFileTransferStatusEventExt>();
}
