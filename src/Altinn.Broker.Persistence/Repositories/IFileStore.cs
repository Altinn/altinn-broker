namespace Altinn.Broker.Persistence
{
    public interface IFileStore
    {
        Task UploadFile(Stream filestream, Guid fileId);
    }    
}