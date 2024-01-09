using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DeleteFileCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

public class ConfirmDownloadCommandHandler : IHandler<ConfirmDownloadCommandRequest, Task>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<ConfirmDownloadCommandHandler> _logger;

    public ConfirmDownloadCommandHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository, IServiceRepository serviceRepository, IBackgroundJobClient backgroundJobClient, ILogger<ConfirmDownloadCommandHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _serviceRepository = serviceRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }
    public async Task<OneOf<Task, Error>> Process(ConfirmDownloadCommandRequest request)
    {
        var service = await _serviceRepository.GetService(request.Token.ClientId);
        if (service is null)
        {
            return Errors.ServiceNotConfigured;
        };
        var file = await _fileRepository.GetFile(request.FileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (!file.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileNotFound;
        }
        if (string.IsNullOrWhiteSpace(file?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }

        await _actorFileStatusRepository.InsertActorFileStatus(request.FileId, ActorFileStatus.DownloadConfirmed, request.Token.Consumer);
        bool shouldConfirmAll = file.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != request.Token.Consumer).All(status => status.Status >= ActorFileStatus.DownloadConfirmed);
        if (shouldConfirmAll)
        {
            await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.AllConfirmedDownloaded);
            _backgroundJobClient.Enqueue<DeleteFileCommandHandler>((deleteFileCommandHandler) => deleteFileCommandHandler.Process(request.FileId));
        }

        return Task.CompletedTask;
    }
}
