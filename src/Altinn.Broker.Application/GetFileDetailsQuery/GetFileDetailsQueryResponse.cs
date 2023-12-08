using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryResponse
{
    public List<ActorFileStatusEntity> ActorEvents { get; set; }
    public List<FileStatusEntity> FileEvents { get; set; }
    public FileEntity File { get; internal set; }
}
