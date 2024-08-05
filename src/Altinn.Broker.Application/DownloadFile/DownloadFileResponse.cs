namespace Altinn.Broker.Application.DownloadFile;
public class DownloadFileResponse
{
    public required string FileName { get; set; }
    public required Stream DownloadStream { get; set; }
}
