using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DeleteFileCommand;
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
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ConfirmDownloadCommandHandler> _logger;

    public ConfirmDownloadCommandHandler(IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository, IAuthorizationService resourceRightsRepository, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<ConfirmDownloadCommandHandler> logger)
    {
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }
    public async Task<OneOf<Task, Error>> Process(ConfirmDownloadCommandRequest request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetFile(request.FileId, cancellationToken);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.FileNotFound;
        };
        if (!file.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileNotFound;
        }
        if (string.IsNullOrWhiteSpace(file?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        if (file.FileStatusEntity.Status != FileStatus.Published)
        {
            return Errors.FileNotPublished;
        }

        await _actorFileStatusRepository.InsertActorFileStatus(request.FileId, ActorFileStatus.DownloadConfirmed, request.Token.Consumer, cancellationToken);
        await _eventBus.Publish(AltinnEventType.DownloadConfirmed, file.ResourceId, file.FileId.ToString(), request.Token.Consumer, cancellationToken);
        bool shouldConfirmAll = file.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != request.Token.Consumer).All(status => status.Status >= ActorFileStatus.DownloadConfirmed);
        if (shouldConfirmAll)
        {
            await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.AllConfirmedDownloaded);
            _backgroundJobClient.Enqueue<DeleteFileCommandHandler>((deleteFileCommandHandler) => deleteFileCommandHandler.Process(request.FileId, cancellationToken));
            await _eventBus.Publish(AltinnEventType.AllConfirmedDownloaded, file.ResourceId, file.FileId.ToString(), file.Sender.ActorExternalId, cancellationToken);
        }

        return Task.CompletedTask;
    }
}
