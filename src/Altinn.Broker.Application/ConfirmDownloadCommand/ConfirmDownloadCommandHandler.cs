using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DeleteFileCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

public class ConfirmDownloadCommandHandler : IHandler<ConfirmDownloadCommandRequest, Task>
{
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<ConfirmDownloadCommandHandler> _logger;

    public ConfirmDownloadCommandHandler(IResourceOwnerRepository resourceOwnerRepository, IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository, IAuthorizationService resourceRightsRepository, IBackgroundJobClient backgroundJobClient, ILogger<ConfirmDownloadCommandHandler> logger)
    {
        _resourceOwnerRepository = resourceOwnerRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }
    public async Task<OneOf<Task, Error>> Process(ConfirmDownloadCommandRequest request, CancellationToken ct)
    {
        var file = await _fileRepository.GetFile(request.FileId, ct);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, request.IsLegacy, ct);
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

        await _actorFileStatusRepository.InsertActorFileStatus(request.FileId, ActorFileStatus.DownloadConfirmed, request.Token.Consumer, ct);
        bool shouldConfirmAll = file.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != request.Token.Consumer).All(status => status.Status >= ActorFileStatus.DownloadConfirmed);
        if (shouldConfirmAll)
        {
            await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.AllConfirmedDownloaded, ct: ct);
            _backgroundJobClient.Enqueue<DeleteFileCommandHandler>((deleteFileCommandHandler) => deleteFileCommandHandler.Process(request.FileId, ct));
        }

        return Task.CompletedTask;
    }
}
