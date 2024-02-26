namespace Altinn.Broker.Repositories
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId, string connectionString, CancellationToken cancellationToken);
        Task<string> UploadFile(Stream filestream, Guid fileId, string connectionString, CancellationToken cancellationToken);
        Task DeleteFile(Guid fileId, string connectionString, CancellationToken cancellationToken);
    }
}
