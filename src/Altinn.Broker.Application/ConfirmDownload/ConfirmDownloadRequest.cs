
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ConfirmDownload;
public class ConfirmDownloadRequest
{
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
    public string? onBehalfOf { get; set; }
}
