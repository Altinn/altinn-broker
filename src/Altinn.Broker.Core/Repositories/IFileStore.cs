namespace Altinn.Broker.Repositories
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId, string connectionString);
        Task<string> UploadFile(Stream filestream, Guid fileId, string connectionString);
        Task DeleteFile(Guid fileId, string connectionString);
    }
}
