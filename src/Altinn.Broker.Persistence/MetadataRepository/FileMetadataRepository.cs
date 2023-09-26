using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Persistence.MetadataRepository
{
    public class FileMetadataRepository : IFileMetadataRepository
    {
        public Task<BrokerFileMetadata> GetBrokerFileMetadata(Guid fileId)
        {
            // ToDo: retrieve file metadata from database
            throw new NotImplementedException();
        }

        public Task<Guid> SaveBrokerFileMetadata(BrokerFileMetadata file)
        {
            // TODO: save file metadata todatabase
            throw new NotImplementedException();
        }

        public void UpdateBrokerFileMetadata(BrokerFileMetadata file)
        {
            // TODO: update file metadata in database
            throw new NotImplementedException();
        }

        public void SetBrokerFileStatus(Guid fileId, BrokerFileStatus fileStatus)
        {
            // TODO: update file status
            throw new NotImplementedException();
        }
    }
}