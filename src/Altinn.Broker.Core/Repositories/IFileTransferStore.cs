namespace Altinn.Broker.Repositories
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId, string connectionString, CancellationToken cancellationToken);
        Task<string> UploadFile(Stream filestream, Guid fileTransferId, string connectionString, CancellationToken cancellationToken);
        Task DeleteFile(Guid fileTransferId, string connectionString, CancellationToken cancellationToken);
    }
}
