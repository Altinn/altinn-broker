namespace Altinn.Broker.Persistence
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId, string? connectionString);
        Task UploadFile(Stream filestream, Guid fileId, string? connectionString);
        Task<bool> IsOnline(string? connectionString);
    }
}
