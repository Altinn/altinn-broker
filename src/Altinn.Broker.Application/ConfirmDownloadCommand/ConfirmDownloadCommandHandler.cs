using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

public class ConfirmDownloadCommandHandler : IHandler<ConfirmDownloadCommandRequest, ConfirmDownloadCommandResponse>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<ConfirmDownloadCommandHandler> _logger;

    public ConfirmDownloadCommandHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, ILogger<ConfirmDownloadCommandHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _logger = logger;
    }
    public async Task<OneOf<ConfirmDownloadCommandResponse, Error>> Process(ConfirmDownloadCommandRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var file = await _fileRepository.GetFile(request.FileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Consumer))
        {
            return Errors.FileNotFound;
        }
        if (string.IsNullOrWhiteSpace(file?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }

        await _fileRepository.AddReceipt(request.FileId, ActorFileStatus.DownloadConfirmed, request.Consumer);
        var recipientStatuses = file.ActorEvents
            .Where(actorEvent => actorEvent.Actor.ActorExternalId != file.Sender && actorEvent.Actor.ActorExternalId != request.Consumer)
            .GroupBy(actorEvent => actorEvent.Actor.ActorExternalId)
            .Select(group => group.Max(statusEvent => statusEvent.Status))
            .ToList();
        bool shouldConfirmAll = recipientStatuses.All(status => status >= ActorFileStatus.DownloadConfirmed);
        await _fileRepository.InsertFileStatus(request.FileId, FileStatus.AllConfirmedDownloaded);

        return new ConfirmDownloadCommandResponse();
    }
}
