using Altinn.Broker.Application.DeleteFileCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.InitializeFileCommand;
public class InitializeFileCommandHandler : IHandler<InitializeFileCommandRequest, Guid>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IResourceManager _resourceManager;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<InitializeFileCommandHandler> _logger;

    public InitializeFileCommandHandler(
        IServiceRepository serviceRepository,
        IServiceOwnerRepository serviceOwnerRepository,
        IFileRepository fileRepository,
        IFileStatusRepository fileStatusRepository,
        IActorFileStatusRepository actorFileStatusRepository,
        IResourceManager resourceManager,
        IBackgroundJobClient backgroundJobClient,
        ILogger<InitializeFileCommandHandler> logger)
    {
        _serviceRepository = serviceRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _resourceManager = resourceManager;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeFileCommandRequest request)
    {
        if (request.Token.Consumer != request.SenderExternalId)
        {
            return Errors.WrongTokenForSender;
        }
        var service = await _serviceRepository.GetService(request.Token.Consumer);
        if (service is null)
        {
            return Errors.ServiceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Token.Supplier);
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        var fileId = await _fileRepository.AddFile(serviceOwner, service, request.Filename, request.SendersFileReference, request.SenderExternalId, request.RecipientExternalIds, request.PropertyList, request.Checksum);
        await _fileStatusRepository.InsertFileStatus(fileId, FileStatus.Initialized);
        var addRecipientEventTasks = request.RecipientExternalIds.Select(recipientId => _actorFileStatusRepository.InsertActorFileStatus(fileId, ActorFileStatus.Initialized, recipientId));
        try
        {
            await Task.WhenAll(addRecipientEventTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed when adding recipient initialized events.");
        }
        _backgroundJobClient.Schedule<DeleteFileCommandHandler>((deleteFileCommandHandler) => deleteFileCommandHandler.Process(fileId), serviceOwner.FileTimeToLive);

        return fileId;
    }
}
