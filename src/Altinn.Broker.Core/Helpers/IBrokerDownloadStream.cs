using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Helpers;
internal interface IBrokerDownloadStream
{
    Task AddManifestFile(FileTransferEntity fileTransferEntity);
}
