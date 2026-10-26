using System.Security.Claims;

using Altinn.Broker.Core.Application;

using OneOf;

namespace Altinn.Broker.Application.UploadPlans;

/// <summary>
/// Returns the plan for continuing a transfer's upload, with how far each part got.
/// </summary>
public class GetUploadPlanHandler(
    UploadPlanAccess uploadPlanAccess,
    IUploadPlanStore uploadPlanStore,
    ITusUploadProgressReader progressReader) : IHandler<GetUploadPlanRequest, GetUploadPlanResponse>
{
    public async Task<OneOf<GetUploadPlanResponse, Error>> Process(GetUploadPlanRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var access = await uploadPlanAccess.GetUploadableFileTransfer(request.FileTransferId, user, cancellationToken);
        if (access.IsT1)
        {
            return access.AsT1 == Errors.FileTransferAlreadyUploaded ? Errors.UploadPlanNotFound : access.AsT1;
        }

        var plan = await uploadPlanStore.TryGetAsync(request.FileTransferId, cancellationToken);
        if (plan is null)
        {
            return Errors.UploadPlanNotFound;
        }

        var parts = new List<GetUploadPlanPartResponse>(plan.Parts.Count);
        foreach (var part in plan.Parts)
        {
            // Nothing recorded reads as not started; the resuming client asks tus for the truth.
            var uploadedBytes = await progressReader.GetUploadedBytesAsync(request.FileTransferId, part.UploadId, cancellationToken);
            parts.Add(new GetUploadPlanPartResponse
            {
                UploadId = part.UploadId,
                Start = part.Start,
                Length = part.Length,
                UploadedBytes = Math.Clamp(uploadedBytes, 0, part.Length),
            });
        }

        return new GetUploadPlanResponse
        {
            Fingerprint = plan.Fingerprint,
            FingerprintVersion = plan.FingerprintVersion,
            UploadedBytes = parts.Sum(part => part.UploadedBytes),
            Parts = parts,
        };
    }
}
