using Hangfire;

namespace Altinn.Broker.Application.UploadFile;

public sealed class TusFinalizeUploadEnqueuer(
    IBackgroundJobClient backgroundJobClient,
    ITusUploadKindResolver tusUploadKindResolver) : ITusFinalizeUploadEnqueuer
{
    public async Task EnqueueAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        var concatJobId = backgroundJobClient.Enqueue<TusConcatenateUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));

        if (await tusUploadKindResolver.IsPartialUploadAsync(tusFileId, cancellationToken))
        {
            return;
        }

        backgroundJobClient.ContinueJobWith<TusPublishUploadHandler>(
            concatJobId,
            handler => handler.Process(fileTransferId, tusFileId, CancellationToken.None));
    }
}
