namespace Altinn.Broker.Persistence
{
    public interface IFileStore
    {
        Task<string> UploadFile(Stream filestream, string shipmentId, string fileReference);
    }
}
