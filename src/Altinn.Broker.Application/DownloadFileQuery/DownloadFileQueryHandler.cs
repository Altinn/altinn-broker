using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryHandler : IHandler<DownloadFileQueryRequest, DownloadFileQueryResponse>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<DownloadFileQueryHandler> _logger;

    public DownloadFileQueryHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IBrokerStorageService brokerStorageService, ILogger<DownloadFileQueryHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<DownloadFileQueryResponse, ActionResult>> Process(DownloadFileQueryRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return new UnauthorizedObjectResult("Service owner not configured for the broker service");
        };
        var file = await _fileRepository.GetFileAsync(request.FileId);
        if (file is null)
        {
            return new NotFoundResult();
        }
        if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Consumer))
        {
            return new NotFoundResult();
        }
        if (string.IsNullOrWhiteSpace(file?.FileLocation))
        {
            return new BadRequestObjectResult("No file uploaded yet");
        }

        var downloadStream = await _brokerStorageService.DownloadFile(serviceOwner, file);
        await _fileRepository.AddReceipt(request.FileId, ActorFileStatus.DownloadStarted, request.Consumer);
        return new DownloadFileQueryResponse()
        {
            Filename = file.Filename,
            Stream = downloadStream
        };
    }
}
