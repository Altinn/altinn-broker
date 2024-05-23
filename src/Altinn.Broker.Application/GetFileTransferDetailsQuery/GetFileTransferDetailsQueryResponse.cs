using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferDetailsQuery;

public class GetFileTransferDetailsQueryResponse
{
    public required List<ActorFileTransferStatusEntity> ActorEvents { get; set; }
    public required List<FileTransferStatusEntity> FileTransferEvents { get; set; }
    public required FileTransferEntity FileTransfer { get; set; }
}
