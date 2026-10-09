namespace Altinn.Broker.Application.ConfirmDownload;
public class ConfirmDownloadRequest
{
    public Guid FileTransferId { get; set; }
    public string? OnBehalfOf { get; set; }
}
