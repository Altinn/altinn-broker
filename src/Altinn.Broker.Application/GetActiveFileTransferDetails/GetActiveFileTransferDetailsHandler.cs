using System.Collections.Concurrent;
using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetActiveFileTransferDetails;

public class GetActiveFileTransferDetailsHandler(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IActorRepository actorRepository,
    IAltinnRegisterService altinnRegisterService,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IResourceRepository resourceRepository,
    IAltinnResourceRepository altinnResourceRepository,
    ILogger<GetActiveFileTransferDetailsHandler> logger) : IHandler<GetActiveFileTransferDetailsRequest, GetActiveFileTransferDetailsResponse>
{
    public async Task<OneOf<GetActiveFileTransferDetailsResponse, Error>> Process(GetActiveFileTransferDetailsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting active file transfers details for {fileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        string? caller = request.OnBehalfOf != string.Empty ? request.OnBehalfOf : user?.GetCallerOrganizationId();
        if (caller is null)
        {
            logger.LogError("Caller not found");
            return Errors.NoAccessToResource;
        }

        var callingActor = await actorRepository.GetActorAsync(caller.WithPrefix(), cancellationToken);
        if (callingActor is null)
        {
            return Errors.NoAccessToResource;
        }
        
       var accessToResource = await authorizationService.CheckAccessForSearch(user, fileTransfer.ResourceId, caller, cancellationToken);
        if (!accessToResource)
        {
            return Errors.NoAccessToResource;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            logger.LogError("Resource not found for file transfer {fileTransferId}", request.FileTransferId);
            
        }

        var resourceResponse = await altinnResourceRepository.GetResourceMetadata(fileTransfer.ResourceId, cancellationToken);
        var resourceName = resourceResponse?.Title ?? fileTransfer.ResourceId;
        var resourceServiceOwner = resourceResponse?.ServiceOwnerName;

        List<FileTransferActor> fileTransferActors = new List<FileTransferActor>()
        {
            new FileTransferActor()
            {
                Sender = fileTransfer.Sender.ActorExternalId,
                Recipients = fileTransfer.RecipientCurrentStatuses.Select(recipient => recipient.Actor.ActorExternalId).ToList()
            }
        };

        var organizationNameById = new ConcurrentDictionary<string, string?>();
        await Parallel.ForEachAsync(
            fileTransferActors.SelectMany(actor => new[] { actor.Sender }.Concat(actor.Recipients)).Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = cancellationToken },
            async (organizationId, ct) =>
            {
                organizationNameById[organizationId] = await altinnRegisterService.LookupOrganizationName(organizationId, ct);
            });
        
        var recipientDetails = fileTransferActors.SelectMany(actor => actor.Recipients).Distinct().Select(recipientId => new RecipientDetails()
        {
            Recipient = recipientId,
            RecipientName = organizationNameById.GetValueOrDefault(recipientId) ?? recipientId,
        }).ToList();

        var fileTransferEvents = await fileTransferStatusRepository.GetFileTransferStatusHistory(request.FileTransferId, cancellationToken);
        var initializedEvent = fileTransferEvents.FirstOrDefault(fileTransferEvent => fileTransferEvent.Status == FileTransferStatus.Initialized);
        var publishedEvent = fileTransferEvents.FirstOrDefault(fileTransferEvent => fileTransferEvent.Status == FileTransferStatus.Published);
        var senderOrgNumber = fileTransfer.Sender.ActorExternalId.WithoutPrefix();

        var isSender = fileTransfer.Sender.ActorExternalId == callingActor.ActorExternalId;
        var callerRecipientStatus = fileTransfer.RecipientCurrentStatuses
            .FirstOrDefault(recipientStatus => recipientStatus.Actor.ActorExternalId == callingActor.ActorExternalId);
        var fileTransferStatus = DetermineFileTransferStatus(fileTransfer, callerRecipientStatus);

        return new GetActiveFileTransferDetailsResponse()
        {
            FileTransferId = fileTransfer.FileTransferId,
            ResourceId = fileTransfer.ResourceId,
            ResourceName = resourceName,
            SendersFileTransferReference = fileTransfer.SendersFileTransferReference,
            FileName = fileTransfer.FileName,
            FileTransferSize = FormatFileSize(fileTransfer.FileTransferSize),
            Created = initializedEvent is not null ? FormatTimestamp(initializedEvent.Date) : null,
            Published = publishedEvent is not null ? FormatTimestamp(publishedEvent.Date) : null,
            UseVirusScan = fileTransfer.UseVirusScan,
            Sender = senderOrgNumber,
            SenderName = organizationNameById.GetValueOrDefault(fileTransfer.Sender.ActorExternalId) ?? senderOrgNumber,
            Recipients = recipientDetails,
            PropertyList = fileTransfer.PropertyList,
            ServiceOwner = resourceServiceOwner ?? fileTransfer.ResourceId,
            ExpirationTime = FormatTimestamp(fileTransfer.ExpirationTime),
            Status = fileTransferStatus,
            IsSender = isSender,
            ActorDownloadStatus = callerRecipientStatus?.Status.ToString()
        };
    }

    private static string FormatTimestamp(DateTimeOffset timestamp) => timestamp.ToString("dd.MM.yyyy HH:mm");

    private static readonly string[] FileSizeUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a raw byte count into a human-readable string with the largest fitting unit, e.g. 2 930 000 000 -> "2.73 GB".
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < FileSizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString(unitIndex == 0 ? "0" : "0.##")} {FileSizeUnits[unitIndex]}";
    }

    /// <summary>
    /// Derives a single frontend-facing status describing what the calling actor should see:
    /// whether the transfer has reached a terminal state (cancelled/purged/failed/all downloaded),
    /// or - while still active - whether the calling actor still needs to download it themselves,
    /// or has already downloaded and is waiting on the remaining recipients.
    /// </summary>
    private static string DetermineFileTransferStatus(FileTransferEntity fileTransfer, ActorFileTransferStatusEntity? callerRecipientStatus)
    {
        switch (fileTransfer.FileTransferStatusEntity.Status)
        {
            case FileTransferStatus.Cancelled:
                return "Cancelled";
            case FileTransferStatus.Purged:
                return "Purged";
            case FileTransferStatus.Failed:
                return "Failed";
            case FileTransferStatus.AllConfirmedDownloaded:
                return "AllDownloaded";
        }

        if (callerRecipientStatus is null)
        {
            return "AwaitingRecipients";
        }

        return callerRecipientStatus.Status == ActorFileTransferStatus.DownloadConfirmed
            ? "AwaitingOtherRecipients"
            : "AwaitingDownloadByCurrentActor";
    }

    private class FileTransferActor
    {
        public string Sender { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
    }
}
