using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.API.Tus;

internal static class TusFinalizeRecovery
{
    public static async Task TryEnqueueFinalizeIfNeededAsync(
        HttpContext httpContext,
        Guid fileTransferId,
        string tusFileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tusFileId))
        {
            return;
        }

        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Altinn.Broker.API.Tus.Recovery");

        var fileTransferRepository = httpContext.RequestServices.GetRequiredService<IFileTransferRepository>();
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return;
        }

        var finalizationService = httpContext.RequestServices.GetRequiredService<ITusUploadFinalizationService>();
        var isPartial = await finalizationService.IsPartialUploadAsync(tusFileId, cancellationToken);
        var currentStatus = fileTransfer.FileTransferStatusEntity.Status;

        if (isPartial)
        {
            if (currentStatus > FileTransferStatus.UploadStarted)
            {
                return;
            }

            if (!await finalizationService.IsReadyForStagingFinalizeAsync(tusFileId, cancellationToken))
            {
                return;
            }
        }
        else
        {
            if (currentStatus != FileTransferStatus.UploadStarted)
            {
                return;
            }

            if (!await finalizationService.IsReadyForTransferCompletionAsync(tusFileId, cancellationToken))
            {
                return;
            }
        }

        logger.LogInformation(
            "Enqueueing TUS finalize job for file transfer {FileTransferId}. TusFileId={TusFileId} IsPartial={IsPartial}",
            fileTransferId,
            tusFileId,
            isPartial);

        var enqueuer = httpContext.RequestServices.GetRequiredService<ITusFinalizeUploadEnqueuer>();
        enqueuer.Enqueue(fileTransferId, tusFileId);
    }
}
