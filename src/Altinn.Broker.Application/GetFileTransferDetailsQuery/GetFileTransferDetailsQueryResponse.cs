using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferDetailsQuery;

public class GetFileTransferDetailsQueryResponse
{
    public List<ActorFileTransferStatusEntity> ActorEvents { get; set; }
    public List<FileTransferStatusEntity> FileTransferEvents { get; set; }
    public FileTransferEntity FileTransfer { get; internal set; }
}
