namespace Altinn.Broker.Repositories
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId, string connectionString, CancellationToken ct);
        Task<string> UploadFile(Stream filestream, Guid fileId, string connectionString, CancellationToken ct);
        Task DeleteFile(Guid fileId, string connectionString, CancellationToken ct);
    }
}
