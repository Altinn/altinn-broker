
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ConfirmDownloadCommand;
public class ConfirmDownloadCommandRequest
{
    public CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
}
