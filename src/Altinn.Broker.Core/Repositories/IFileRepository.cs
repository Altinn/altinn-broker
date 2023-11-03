using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    Task<Guid> AddFileAsync(Core.Domain.File file);
    Task AddReceiptAsync(ActorFileStatus receipt);
    Task<Domain.File?> GetFileAsync(Guid fileId);
    Task<List<string>> GetFilesAvailableForCaller(string actorExernalReference);

    Task AddReceipt(
        Guid fileId, 
        Domain.Enums.ActorFileStatus status, 
        string actorExternalReference
    );

    Task SetStorageReference(
        Guid fileId,
        string storageReference
    );
    List<FileStatusEntity> GetFileStatusHistory(Guid fileId);
    List<ActorFileStatus> GetFileRecipientStatusHistory(Guid fileId);
}