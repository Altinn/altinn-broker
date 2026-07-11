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
        var partialUploadRegistry = httpContext.RequestServices.GetRequiredService<ITusPartialUploadRegistry>();
        var concatJobCoordinator = httpContext.RequestServices.GetRequiredService<ITusConcatJobCoordinator>();
        var storageResolver = httpContext.RequestServices.GetRequiredService<ITusStorageResolver>();
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

            logger.LogInformation(
                "Enqueueing TUS partial finalize job for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);

            var enqueuer = httpContext.RequestServices.GetRequiredService<ITusFinalizeUploadEnqueuer>();
            await enqueuer.EnqueueConcatenateAsync(fileTransferId, tusFileId, cancellationToken);
            return;
        }

        if (currentStatus is not (FileTransferStatus.UploadStarted or FileTransferStatus.UploadProcessing))
        {
            return;
        }

        var concatStatus = await partialUploadRegistry.TryGetConcatStatusAsync(tusFileId, cancellationToken);
        if (concatStatus == TusConcatStatus.Complete)
        {
            if (await storageResolver.DestinationBlobExistsAsync(tusFileId, cancellationToken))
            {
                return;
            }

            logger.LogInformation(
                "Enqueueing TUS publish job for file transfer {FileTransferId}. Concat already complete.",
                fileTransferId);

            httpContext.RequestServices.GetRequiredService<ITusFinalizeUploadEnqueuer>()
                .EnqueuePublish(fileTransferId, tusFileId);
            return;
        }

        if (await partialUploadRegistry.TryGetFinalConcatPartialIdsAsync(tusFileId, cancellationToken) is not null
            || concatStatus is TusConcatStatus.Pending or TusConcatStatus.InProgress)
        {
            if (concatStatus == TusConcatStatus.InProgress
                && await concatJobCoordinator.IsConcatRunningAsync(tusFileId, cancellationToken))
            {
                return;
            }

            logger.LogInformation(
                "Enqueueing TUS concatenate job for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);

            var enqueuer = httpContext.RequestServices.GetRequiredService<ITusFinalizeUploadEnqueuer>();
            await enqueuer.EnqueueConcatenateAsync(fileTransferId, tusFileId, cancellationToken);
            return;
        }

        if (!await finalizationService.IsReadyForTransferCompletionAsync(tusFileId, cancellationToken))
        {
            return;
        }

        logger.LogInformation(
            "Enqueueing TUS finalize jobs for file transfer {FileTransferId}. TusFileId={TusFileId}",
            fileTransferId,
            tusFileId);

        var finalizeEnqueuer = httpContext.RequestServices.GetRequiredService<ITusFinalizeUploadEnqueuer>();
        await finalizeEnqueuer.EnqueueConcatenateAsync(fileTransferId, tusFileId, cancellationToken);
    }
}
