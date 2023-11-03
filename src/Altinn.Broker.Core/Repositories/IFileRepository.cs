using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    Task<Guid> AddFileAsync(FileEntity file);
    Task AddReceiptAsync(ActorFileStatusEntity receipt);
    Task<Domain.FileEntity?> GetFileAsync(Guid fileId);
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
    Task<List<FileStatusEntity>> GetFileStatusHistoryAsync(Guid fileId);
    Task<List<ActorFileStatusEntity>> GetFileRecipientStatusHistoryAsync(Guid fileId);
}