using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Core.Services
{
    public class FileService : IFileService
    {
        public Task<BrokerFileMetadata> CancelFile(Guid fileId)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileMetadata> ConfirmDownload(Guid fileId)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> DownloadFile(Guid fileId)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileMetadata> GetBrokerFileMetadata(Guid fileId)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileStatusOverview> GetFileStatus(Guid fileId)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileMetadata> OverwriteFile(Guid fileId, Stream fileStream, string fileName, string fileReference, string checksum)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileMetadata> ResumeUploadFile(Guid shipmentId, Guid fileId, Stream fileStream, string fileName, string fileReference, string checksum)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> SaveBrokerFileMetadata(BrokerFileMetadata file)
        {
            throw new NotImplementedException();
        }

        public void SetBrokerFileStatus(Guid fileId, BrokerFileStatus fileStatus)
        {
            throw new NotImplementedException();
        }

        public void UpdateBrokerFileMetadata(BrokerFileMetadata file)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileMetadata> UploadFile(Guid shipmentId, Stream fileStream, string fileName, string fileReference, string checksum)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerFileStatusOverview> UploadFile(Guid shipmentId, Guid FileId, Stream filestream)
        {

            throw new NotImplementedException();
        }
    }
}
