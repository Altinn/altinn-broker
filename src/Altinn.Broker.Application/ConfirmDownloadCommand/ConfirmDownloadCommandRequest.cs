
namespace Altinn.Broker.Application.ConfirmDownloadCommand;
public class ConfirmDownloadCommandRequest
{
    public Guid FileId { get; set; }
    public string Consumer { get; set; }
    public string Supplier { get; set; }
    public string ClientId { get; set; }
}
