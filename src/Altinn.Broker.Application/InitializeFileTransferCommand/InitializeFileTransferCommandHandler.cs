﻿using Altinn.Broker.Application.ExpireFileTransferCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.InitializeFileTransferCommand;
public class InitializeFileTransferCommandHandler : IHandler<InitializeFileTransferCommandRequest, Guid>
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IActorFileTransferStatusRepository _actorFileTransferStatusRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<InitializeFileTransferCommandHandler> _logger;

    public InitializeFileTransferCommandHandler(
        IResourceRepository resourceRepository,
        IServiceOwnerRepository serviceOwnerRepository,
        IAuthorizationService resourceRightsRepository,
        IFileTransferRepository fileTransferRepository,
        IFileTransferStatusRepository fileTransferStatusRepository,
        IActorFileTransferStatusRepository actorFileTransferStatusRepository,
        IBackgroundJobClient backgroundJobClient,
        IEventBus eventBus,
        ILogger<InitializeFileTransferCommandHandler> logger)
    {
        _resourceRepository = resourceRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _actorFileTransferStatusRepository = actorFileTransferStatusRepository;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeFileTransferCommandRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var resource = await _resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        var fileTransferId = await _fileTransferRepository.AddFileTransfer(serviceOwner, resource, request.FileName, request.SendersFileTransferReference, request.SenderExternalId, request.RecipientExternalIds, request.PropertyList, request.Checksum, null, null, cancellationToken);
        await _fileTransferStatusRepository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: cancellationToken);
        var addRecipientEventTasks = request.RecipientExternalIds.Select(recipientId => _actorFileTransferStatusRepository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, recipientId, cancellationToken));
        try
        {
            await Task.WhenAll(addRecipientEventTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed when adding recipient initialized events.");
        }
        var jobId = _backgroundJobClient.Schedule<ExpireFileTransferCommandHandler>((ExpireFileTransferCommandHandler) => ExpireFileTransferCommandHandler.Process(new ExpireFileTransferCommandRequest
        {
            FileTransferId = fileTransferId,
            Force = false
        }, cancellationToken), serviceOwner.FileTransferTimeToLive);
        await _fileTransferRepository.SetFileTransferHangfireJobId(fileTransferId, jobId, cancellationToken);
        await _eventBus.Publish(AltinnEventType.FileTransferInitialized, request.ResourceId, fileTransferId.ToString(), request.SenderExternalId, cancellationToken);

        return fileTransferId;
    }
}

