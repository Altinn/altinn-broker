namespace Altinn.Broker.Persistence
{
    public interface IFileStore
    {
        Task<Stream> GetFileStream(Guid fileId);
        Task UploadFile(Stream filestream, Guid fileId);
    }
}