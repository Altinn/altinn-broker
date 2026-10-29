using System.Security.Claims;

using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Core.Application;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadPlans;

/// <summary>
/// Keeps the plan a client made for uploading a transfer's file, so that the upload can be continued
/// from another browser. A split file's parts have to be partial uploads tus holds for this transfer.
/// </summary>
public class SaveUploadPlanHandler(
    UploadPlanAccess uploadPlanAccess,
    IUploadPlanStore uploadPlanStore,
    ITusPartialUploadLookup partialUploadLookup,
    ILogger<SaveUploadPlanHandler> logger) : IHandler<SaveUploadPlanRequest, Task>
{
    // Far above what any client splits a file into, but bounded all the same.
    private const int MaxParts = 64;
    private const int MaxFingerprintLength = 256;

    public async Task<OneOf<Task, Error>> Process(SaveUploadPlanRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var access = await uploadPlanAccess.GetUploadableFileTransfer(request.FileTransferId, user, cancellationToken);
        if (access.IsT1)
        {
            return access.AsT1;
        }

        if (!HasValidShape(request) || !await PartsAreUploadsOfTheTransfer(request, cancellationToken))
        {
            return Errors.InvalidUploadPlan;
        }

        await uploadPlanStore.SaveAsync(
            request.FileTransferId,
            new UploadPlan(
                request.Fingerprint,
                request.FingerprintVersion,
                request.Parts.Select(part => new UploadPlanPart(part.UploadId, part.Start, part.Length)).ToList(),
                DateTimeOffset.UtcNow),
            cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>The parts follow each other from the first byte, with no gaps or overlaps.</summary>
    private static bool HasValidShape(SaveUploadPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Fingerprint)
            || request.Fingerprint.Length > MaxFingerprintLength
            || request.FingerprintVersion <= 0
            || request.Parts.Count is 0 or > MaxParts
            || request.Parts.Select(part => part.UploadId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Parts.Count)
        {
            return false;
        }

        long expectedStart = 0;
        foreach (var part in request.Parts)
        {
            if (string.IsNullOrWhiteSpace(part.UploadId) || part.Start != expectedStart || part.Length <= 0)
            {
                return false;
            }
            expectedStart += part.Length;
        }

        return true;
    }

    /// <summary>
    /// A file sent whole is one upload under the transfer's own id; tus answers for it when the upload
    /// continues. A split file is partial uploads only, each one created for this transfer with the
    /// length the plan gives it. Partials the plan leaves out, such as one a retried create left behind,
    /// are no concern of the plan.
    /// </summary>
    private async Task<bool> PartsAreUploadsOfTheTransfer(SaveUploadPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.Parts.Count == 1 && IsTransferId(request.Parts[0].UploadId, request.FileTransferId))
        {
            return true;
        }

        var partialLengths = (await partialUploadLookup.GetPartialUploadsAsync(request.FileTransferId, cancellationToken))
            .ToDictionary(partial => partial.PartialUploadId, partial => partial.UploadLength, StringComparer.OrdinalIgnoreCase);
        foreach (var part in request.Parts)
        {
            if (!partialLengths.TryGetValue(part.UploadId, out var length) || length != part.Length)
            {
                logger.LogInformation(
                    "Rejected upload plan for file transfer {FileTransferId}: part {UploadId} is not a partial upload of it with length {Length}",
                    request.FileTransferId,
                    part.UploadId,
                    part.Length);
                return false;
            }
        }

        return true;
    }

    private static bool IsTransferId(string uploadId, Guid fileTransferId)
        => Guid.TryParse(uploadId, out var uploadGuid) && uploadGuid == fileTransferId;
}
