using System.Transactions;

using Altinn.Broker.Application.ExpireFileTransfer;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

using Polly;

using Serilog.Context;

namespace Altinn.Broker.Application.InitializeFileTransfer;
public class InitializeFileTransferHandler : IHandler<InitializeFileTransferRequest, Guid>
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IActorFileTransferStatusRepository _actorFileTransferStatusRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<InitializeFileTransferHandler> _logger;

    public InitializeFileTransferHandler(
        IResourceRepository resourceRepository,
        IServiceOwnerRepository serviceOwnerRepository,
        IAuthorizationService resourceRightsRepository,
        IFileTransferRepository fileTransferRepository,
        IFileTransferStatusRepository fileTransferStatusRepository,
        IActorFileTransferStatusRepository actorFileTransferStatusRepository,
        IBackgroundJobClient backgroundJobClient,
        IEventBus eventBus,
        ILogger<InitializeFileTransferHandler> logger)
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

    public async Task<OneOf<Guid, Error>> Process(InitializeFileTransferRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var resource = await _resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        var fileExpirationTime = DateTime.UtcNow.Add(resource.FileTransferTimeToLive ?? TimeSpan.FromDays(30));
        var fileTransferId = await _fileTransferRepository.AddFileTransfer(serviceOwner, resource, request.FileName, request.SendersFileTransferReference, request.SenderExternalId, request.RecipientExternalIds, fileExpirationTime, request.PropertyList, request.Checksum, null, null, cancellationToken);
        LogContext.PushProperty("fileTransferId", fileTransferId);        
        var addRecipientEventTasks = request.RecipientExternalIds.Select(recipientId => _actorFileTransferStatusRepository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, recipientId, cancellationToken));
        try
        {
            await Task.WhenAll(addRecipientEventTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed when adding recipient initialized events: {message}\n{stackTrace}", ex.Message, ex.StackTrace);
            throw;
        }
        var jobId = _backgroundJobClient.Schedule<ExpireFileTransferHandler>((ExpireFileTransferHandler) => ExpireFileTransferHandler.Process(new ExpireFileTransferRequest
        {
            FileTransferId = fileTransferId,
            Force = false
        }, cancellationToken), fileExpirationTime);
        await _fileTransferRepository.SetFileTransferHangfireJobId(fileTransferId, jobId, cancellationToken);
        var retryResult = await TransactionPolicy.RetryPolicy(_logger).ExecuteAndCaptureAsync(async () =>
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await _fileTransferStatusRepository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: cancellationToken);
                await _eventBus.Publish(AltinnEventType.FileTransferInitialized, resource.Id, fileTransferId.ToString(), request.SenderExternalId, cancellationToken);
                transaction.Complete();
                return fileTransferId;
            }
        });
        if (retryResult.Outcome == OutcomeType.Failure)
        {
            throw retryResult.FinalException;
        } else
        {
            return retryResult.Result;
        }
    }
}

