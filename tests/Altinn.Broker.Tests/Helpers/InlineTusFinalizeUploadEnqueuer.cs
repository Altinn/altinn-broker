using Altinn.Broker.Application.UploadFile;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Runs TUS finalize inline so integration tests do not depend on a Hangfire worker.
/// </summary>
internal sealed class InlineTusFinalizeUploadEnqueuer(IServiceScopeFactory serviceScopeFactory) : ITusFinalizeUploadEnqueuer
{
    public void Enqueue(Guid fileTransferId, string tusFileId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<TusFinalizeUploadHandler>();
        handler.Process(fileTransferId, tusFileId, CancellationToken.None).GetAwaiter().GetResult();
    }
}
