using System.Xml;

using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownload;
using Altinn.Broker.Application.ExpireFileTransfer;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OneOf;

public class ConfirmDownloadHandler : IHandler<ConfirmDownloadRequest, Task>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IActorFileTransferStatusRepository _actorFileTransferStatusRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ConfirmDownloadHandler> _logger;
    private readonly string _defaultGracePeriod;

    public ConfirmDownloadHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, IResourceRepository resourceRepository, IAuthorizationService resourceRightsRepository, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<ConfirmDownloadHandler> logger, IOptions<ApplicationSettings> applicationSettings)
    {
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _actorFileTransferStatusRepository = actorFileTransferStatusRepository;
        _resourceRepository = resourceRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
        _defaultGracePeriod = applicationSettings.Value.DefaultGracePeriod;
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
                var resource = await _resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
                _backgroundJobClient.Delete(fileTransfer.HangfireJobId);
                if (resource!.PurgeFileTransferAfterAllRecipientsConfirmed)
                {

                    _backgroundJobClient.Enqueue<ExpireFileTransferHandler>((expireFileTransferHandler) => expireFileTransferHandler.Process(new ExpireFileTransferRequest
                    {
                        FileTransferId = request.FileTransferId,
                        Force = true
                    }, cancellationToken));
                }
                else
                {
                    var gracePeriod = resource.PurgeFileTransferGracePeriod ?? XmlConvert.ToTimeSpan(_defaultGracePeriod);
                    _backgroundJobClient.Schedule<ExpireFileTransferHandler>((expireFileTransferHandler) => expireFileTransferHandler.Process(new ExpireFileTransferRequest
                    {
                        FileTransferId = request.FileTransferId,
                        Force = false
                    }, cancellationToken), DateTime.UtcNow.Add(gracePeriod).AddMinutes(1));
                }
            }
            return Task.CompletedTask;
        }, _logger, cancellationToken);
    }
}
