namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryResponse
{
    public required string FileName { get; set; }
    public required Stream DownloadStream { get; set; }
}
