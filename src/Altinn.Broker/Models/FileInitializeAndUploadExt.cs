namespace Altinn.Broker.Models;

public class FileInitializeAndUploadExt
{
    public FileInitalizeExt Metadata { get; set; } 

    public IFormFile File { get; set; }
}
