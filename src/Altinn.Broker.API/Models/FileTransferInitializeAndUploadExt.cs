namespace Altinn.Broker.Models;

public class FileTransferInitializeAndUploadExt
{
    public FileTransferInitalizeExt Metadata { get; set; }

    public IFormFile FileTransfer { get; set; }
}
