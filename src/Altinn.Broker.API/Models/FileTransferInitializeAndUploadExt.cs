namespace Altinn.Broker.Models;

public class FileTransferInitializeAndUploadExt
{
    public required FileTransferInitalizeExt Metadata { get; set; }

    public required IFormFile FileTransfer { get; set; }
}
