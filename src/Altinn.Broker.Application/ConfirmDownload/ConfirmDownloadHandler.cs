using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownload;
using Altinn.Broker.Application.ExpireFileTransfer;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

public class ConfirmDownloadHandler : IHandler<ConfirmDownloadRequest, Task>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IActorFileTransferStatusRepository _actorFileTransferStatusRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ConfirmDownloadHandler> _logger;

    public ConfirmDownloadHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, IAuthorizationService resourceRightsRepository, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<ConfirmDownloadHandler> logger)
    {
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _actorFileTransferStatusRepository = actorFileTransferStatusRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }
    public async Task<OneOf<Task, Error>> Process(ConfirmDownloadRequest request, CancellationToken cancellationToken)
    {
        var fileTransfer = await _fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.FileTransferNotFound;
        };
        if (!fileTransfer.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileTransferNotFound;
        }
        if (string.IsNullOrWhiteSpace(fileTransfer?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        if (fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.Published)
        {
            return Errors.FileTransferNotPublished;
        }
        if (fileTransfer.RecipientCurrentStatuses.First(recipientStatus => recipientStatus.Actor.ActorExternalId == request.Token.Consumer).Status == ActorFileTransferStatus.DownloadConfirmed)
        {
            return Task.CompletedTask;
        }
        if (fileTransfer.RecipientCurrentStatuses.First(recipientStatus => recipientStatus.Actor.ActorExternalId == request.Token.Consumer).Status == ActorFileTransferStatus.DownloadConfirmed)
        {
            return Task.CompletedTask;
        }
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            await _eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), request.Token.Consumer, cancellationToken);
            await _eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            await _actorFileTransferStatusRepository.InsertActorFileTransferStatus(request.FileTransferId, ActorFileTransferStatus.DownloadConfirmed, request.Token.Consumer, cancellationToken);
            bool shouldConfirmAll = fileTransfer.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != request.Token.Consumer).All(status => status.Status >= ActorFileTransferStatus.DownloadConfirmed);
            if (shouldConfirmAll)
            {
                await _eventBus.Publish(AltinnEventType.AllConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
                await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.AllConfirmedDownloaded);
                _backgroundJobClient.Enqueue<ExpireFileTransferHandler>((expireFileTransferHandler) => expireFileTransferHandler.Process(new ExpireFileTransferRequest
                {
                    FileTransferId = request.FileTransferId,
                    Force = true
                }, cancellationToken));
            }
            return Task.CompletedTask;
        }, _logger, cancellationToken);
    }
}
