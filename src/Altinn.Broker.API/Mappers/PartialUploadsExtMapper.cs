using Altinn.Broker.API.Models;
using Altinn.Broker.Application.UploadFile.Tus;

namespace Altinn.Broker.Mappers;

internal static class PartialUploadsExtMapper
{
    internal static PartialUploadsExt MapToExternalModel(IReadOnlyList<TusPartialUpload> partialUploads)
    {
        return new PartialUploadsExt()
        {
            PartialUploads = partialUploads
                .Select(partialUpload => new PartialUploadExt()
                {
                    PartialUploadId = partialUpload.PartialUploadId,
                    UploadLength = partialUpload.UploadLength
                })
                .ToList()
        };
    }
}
