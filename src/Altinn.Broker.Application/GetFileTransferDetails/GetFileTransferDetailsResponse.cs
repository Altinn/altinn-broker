using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferDetails;

public class GetFileTransferDetailsResponse
{
    public required List<ActorFileTransferStatusEntity> ActorEvents { get; set; }
    public required List<FileTransferStatusEntity> FileTransferEvents { get; set; }
    public required FileTransferEntity FileTransfer { get; set; }
}
