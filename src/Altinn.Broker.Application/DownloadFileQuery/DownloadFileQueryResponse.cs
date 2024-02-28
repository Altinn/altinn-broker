namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryResponse
{
    public string FileName { get; set; }
    public Stream DownloadStream { get; set; }
}
