using Altinn.Broker.Application.UploadFile;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Runs TUS finalize inline so integration tests do not depend on a Hangfire worker.
/// </summary>
internal sealed class InlineTusFinalizeUploadEnqueuer(IServiceScopeFactory serviceScopeFactory) : ITusFinalizeUploadEnqueuer
{
    public async Task EnqueueAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var concatenateHandler = scope.ServiceProvider.GetRequiredService<TusConcatenateUploadHandler>();
        var publishHandler = scope.ServiceProvider.GetRequiredService<TusPublishUploadHandler>();
        var uploadKindResolver = scope.ServiceProvider.GetRequiredService<ITusUploadKindResolver>();

        await concatenateHandler.Process(fileTransferId, tusFileId, cancellationToken);

        if (!await uploadKindResolver.IsPartialUploadAsync(tusFileId, cancellationToken))
        {
            await publishHandler.Process(fileTransferId, tusFileId, cancellationToken);
        }
    }
}
