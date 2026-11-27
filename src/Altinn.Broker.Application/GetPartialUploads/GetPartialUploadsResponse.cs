using Altinn.Broker.Application.UploadFile.Tus;

namespace Altinn.Broker.Application.GetPartialUploads;

public class GetPartialUploadsResponse
{
    public required IReadOnlyList<TusPartialUpload> PartialUploads { get; set; }
}
