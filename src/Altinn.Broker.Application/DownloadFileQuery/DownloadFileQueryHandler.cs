using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryHandler : IHandler<DownloadFileQueryRequest, DownloadFileQueryResponse>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<DownloadFileQueryHandler> _logger;

    public DownloadFileQueryHandler(IServiceRepository serviceRepository, IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IActorFileStatusRepository actorFileStatusRepository, IBrokerStorageService brokerStorageService, ILogger<DownloadFileQueryHandler> logger)
    {
        _serviceRepository = serviceRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<DownloadFileQueryResponse, Error>> Process(DownloadFileQueryRequest request)
    {
        var service = await _serviceRepository.GetService(request.Token.ClientId);
        if (service is null)
        {
            return Errors.ServiceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(service.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
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
        var downloadStream = await _brokerStorageService.DownloadFile(serviceOwner, file);
        await _actorFileStatusRepository.InsertActorFileStatus(request.FileId, ActorFileStatus.DownloadStarted, request.Token.Consumer);
        return new DownloadFileQueryResponse()
        {
            Filename = file.Filename,
            Stream = downloadStream
        };
    }
}
