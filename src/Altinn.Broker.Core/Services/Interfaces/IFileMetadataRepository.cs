using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{    
    public interface IFileMetadataRepository
    {
        Task<BrokerFileMetadata> GetBrokerFileMetadata(Guid fileId);
        Task<Guid> SaveBrokerFileMetadata(BrokerFileMetadata file);
        void UpdateBrokerFileMetadata(BrokerFileMetadata file);
        void SetBrokerFileStatus(Guid fileId, BrokerFileStatus fileStatus);
    }    
}