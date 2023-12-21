namespace Altinn.Broker.Repositories
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId, string connectionString);
        Task UploadFile(Stream filestream, Guid fileId, string connectionString);
        Task DeleteFile(Guid fileId, string connectionString);
    }
}
