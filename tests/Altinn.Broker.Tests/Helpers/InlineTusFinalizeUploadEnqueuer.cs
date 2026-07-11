using Altinn.Broker.Application.UploadFile;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Runs TUS finalize inline so integration tests do not depend on a Hangfire worker.
/// </summary>
internal sealed class InlineTusFinalizeUploadEnqueuer(IServiceScopeFactory serviceScopeFactory) : ITusFinalizeUploadEnqueuer
{
    public async Task<bool> EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var concatenateHandler = scope.ServiceProvider.GetRequiredService<TusConcatenateUploadHandler>();
        await concatenateHandler.Process(fileTransferId, tusFileId, cancellationToken);
        return true;
    }

    public bool EnqueuePublish(Guid fileTransferId, string tusFileId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var publishHandler = scope.ServiceProvider.GetRequiredService<TusPublishUploadHandler>();
        publishHandler.Process(fileTransferId, tusFileId, CancellationToken.None).GetAwaiter().GetResult();
        return true;
    }
}
