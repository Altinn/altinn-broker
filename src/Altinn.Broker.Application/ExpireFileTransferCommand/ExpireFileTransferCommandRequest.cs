

using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ExpireFileTransferCommand;

public class ExpireFileTransferCommandRequest
{
    public Guid FileTransferId { get; set; }
    public bool Force { get; set; }
}
