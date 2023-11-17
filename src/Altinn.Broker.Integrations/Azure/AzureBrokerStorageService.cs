using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

namespace Altinn.Broker.Integrations.Azure;
public class AzureBrokerStorageService : IBrokerStorageService
{
    public Task UploadFile(ServiceOwnerEntity? serviceOwnerEntity, FileEntity fileEntity, Stream stream)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadFile(ServiceOwnerEntity? serviceOwnerEntity, FileEntity file)
    {
        throw new NotImplementedException();
    }
}
