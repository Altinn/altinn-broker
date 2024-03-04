using Altinn.Broker.Application.ExpireFileTransferCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UpdateFileRetention;

public class UpdateFileRetentionHandler : IHandler<UpdateFileRetentionRequest, Task>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<UpdateFileRetentionHandler> _logger;

    public UpdateFileRetentionHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IServiceOwnerRepository serviceOwnerRepository, IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IBrokerStorageService brokerStorageService, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<UpdateFileRetentionHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _resourceRepository = resourceRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _brokerStorageService = brokerStorageService;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<OneOf<Task, Error>> Process(UpdateFileRetentionRequest request, CancellationToken cancellationToken)
    {

        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.ServiceOwnerId);
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };

        var fileTransfers = await _fileTransferRepository.GetNonDeletedFileTransfersByStorageProvider(serviceOwner.StorageProvider.Id, cancellationToken);
        foreach (var fileTransfer in fileTransfers)
        {
            _backgroundJobClient.Enqueue<ExpireFileTransferCommandHandler>(handler => handler.RescheduleExpireEvent(new ExpireFileTransferCommandRequest
            {
                FileTransferId = fileTransfer.FileTransferId,
                Force = false
            }, CancellationToken.None));
        }
        return Task.CompletedTask;
    }
}
