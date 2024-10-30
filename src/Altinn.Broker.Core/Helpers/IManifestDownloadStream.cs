using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Helpers;
internal interface IManifestDownloadStream
{
    Task AddManifestFile(FileTransferEntity fileTransferEntity);
}
