using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DeleteFileTransferCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

public class ConfirmDownloadCommandHandler : IHandler<ConfirmDownloadCommandRequest, Task>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IActorFileTransferStatusRepository _actorFileTransferStatusRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ConfirmDownloadCommandHandler> _logger;

    public ConfirmDownloadCommandHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, IAuthorizationService resourceRightsRepository, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<ConfirmDownloadCommandHandler> logger)
    {
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _actorFileTransferStatusRepository = actorFileTransferStatusRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }
    public async Task<OneOf<Task, Error>> Process(ConfirmDownloadCommandRequest request, CancellationToken cancellationToken)
    {
        var fileTransfer = await _fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
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

        await _actorFileTransferStatusRepository.InsertActorFileTransferStatus(request.FileTransferId, ActorFileTransferStatus.DownloadConfirmed, request.Token.Consumer, cancellationToken);
        await _eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), cancellationToken);
        bool shouldConfirmAll = fileTransfer.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != request.Token.Consumer).All(status => status.Status >= ActorFileTransferStatus.DownloadConfirmed);
        if (shouldConfirmAll)
        {
            await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.AllConfirmedDownloaded);
            _backgroundJobClient.Enqueue<DeleteFileTransferCommandHandler>((deleteFileTransferCommandHandler) => deleteFileTransferCommandHandler.Process(request.FileTransferId, cancellationToken));
            await _eventBus.Publish(AltinnEventType.AllConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), cancellationToken);
        }

        return Task.CompletedTask;
    }
}
