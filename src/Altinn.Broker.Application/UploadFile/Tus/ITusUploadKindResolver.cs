namespace Altinn.Broker.Application.UploadFile.Tus;

public interface ITusUploadKindResolver
{
    Task<bool> IsPartialUploadAsync(string tusFileId, CancellationToken cancellationToken);
}
