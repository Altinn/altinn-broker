
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ConfirmDownloadCommand;
public class ConfirmDownloadCommandRequest
{
    public MaskinportenToken Token { get; set; }
    public Guid FileId { get; set; }
}
