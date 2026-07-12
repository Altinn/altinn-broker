namespace Altinn.Broker.Application.UploadFile;

public interface ITusUploadKindResolver
{
    Task<bool> IsPartialUploadAsync(string tusFileId, CancellationToken cancellationToken);
}
