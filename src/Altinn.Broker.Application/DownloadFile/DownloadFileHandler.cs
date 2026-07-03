using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DownloadFile;
public class DownloadFileHandler(IResourceRepository resourceRepository, IServiceOwnerRepository serviceOwnerRepository, IAuthorizationService authorizationService, IFileTransferRepository fileTransferRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, IBrokerStorageService brokerStorageService, IDistributedCache distributedCache, ILogger<DownloadFileHandler> logger) : IHandler<DownloadFileRequest, DownloadFileResponse>
{
    private static readonly TimeSpan RangedDownloadStatusDebounceWindow = TimeSpan.FromMinutes(5);

    public async Task<OneOf<DownloadFileResponse, Error>> Process(DownloadFileRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting download of file transfer {FileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsRecipient(user, fileTransfer, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (request.IsLegacy && request.OnBehalfOfConsumer is not null && !fileTransfer.IsRecipient(request.OnBehalfOfConsumer))
        {
            return Errors.NoAccessToResource;
        }
        if (fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.Published && fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.AllConfirmedDownloaded)
        {
            return Errors.FileTransferNotAvailable;
        }
        if (string.IsNullOrWhiteSpace(fileTransfer?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        }
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        ByteRange? resolvedRange = null;
        if (request.Range is not null)
        {
            resolvedRange = request.Range.Value.Resolve(fileTransfer.FileTransferSize);
            if (resolvedRange is null)
            {
                return Errors.InvalidByteRange;
            }
        }
        var download = await brokerStorageService.DownloadFile(serviceOwner, fileTransfer, resolvedRange, cancellationToken);
        var downloadStream = download.Content;
        if (resource.UseManifestFileShim == true && request.IsLegacy)
        {
            var fileBuffer = new byte[fileTransfer.FileTransferSize];
            downloadStream.ReadExactly(fileBuffer, 0, fileBuffer.Length);
            downloadStream = new ManifestDownloadStream(fileBuffer);
            await ((ManifestDownloadStream)downloadStream).AddManifestFile(fileTransfer, resource);
        }
        var caller = request.OnBehalfOfConsumer ?? user?.GetCallerOrganizationId();
        var vendor = user?.GetCallerVendorId()?.WithPrefix();
        await InsertDownloadStartedStatus(request.FileTransferId, isRangedDownload: resolvedRange is not null, caller.WithPrefix(), vendor, cancellationToken);
        return new DownloadFileResponse()
        {
            FileName = fileTransfer.FileName,
            DownloadStream = downloadStream,
            TotalSize = download.TotalLength,
            ResolvedRange = resolvedRange
        };
    }

    private async Task InsertDownloadStartedStatus(Guid fileTransferId, bool isRangedDownload, string caller, string? vendor, CancellationToken cancellationToken)
    {
        var debounceCacheKey = $"download-started:{fileTransferId}:{caller}";
        if (isRangedDownload)
        {
            try
            {
                if (await distributedCache.GetAsync(debounceCacheKey, cancellationToken) is not null)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to read DownloadStarted debounce cache for file transfer {FileTransferId}", fileTransferId);
            }
        }
        await actorFileTransferStatusRepository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadStarted, caller, vendor, cancellationToken);
        if (isRangedDownload)
        {
            try
            {
                await distributedCache.SetAsync(debounceCacheKey, new byte[] { 1 }, new DistributedCacheEntryOptions
                {
                    SlidingExpiration = RangedDownloadStatusDebounceWindow
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to write DownloadStarted debounce cache for file transfer {FileTransferId}", fileTransferId);
            }
        }
    }
}
