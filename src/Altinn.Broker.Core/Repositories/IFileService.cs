using System.Diagnostics.CodeAnalysis;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{    
    public interface IFileService
    {
        Task<BrokerFileStatusOverview> UploadFile(Guid fileId, Stream fileStream);
        Task<BrokerFileStatusOverview> GetFileStatus(Guid fileId);
        Task<BrokerFileMetadata> GetBrokerFileMetadata(Guid fileId);
        Task<Stream> DownloadFile(Guid fileId);
        Task<Guid> SaveBrokerFileMetadata(BrokerFileMetadata file);
        void UpdateBrokerFileMetadata(BrokerFileMetadata file);
        void SetBrokerFileStatus(Guid fileId, BrokerFileStatus fileStatus);
        Task<BrokerFileMetadata> CancelFile(Guid fileId);
        Task<BrokerFileMetadata> ConfirmDownload(Guid fileId);
        Task<BrokerFileMetadata> UploadFile(Stream fileStream, string fileName, string fileReference, string checksum);
        Task<BrokerFileMetadata> ResumeUploadFile(Guid fileId, Stream fileStream, string fileName, string fileReference, string checksum);
        Task<BrokerFileMetadata> OverwriteFile(Guid fileId, Stream fileStream, string fileName, string fileReference, string checksum);
    }    
}