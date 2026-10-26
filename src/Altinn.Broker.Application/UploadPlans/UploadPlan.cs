namespace Altinn.Broker.Application.UploadPlans;

/// <summary>
/// How a sender's client split a file into tus uploads, kept so the upload can be continued from
/// another browser. The fingerprint is opaque to the server: only the client that made it can check it.
/// The file's name is the transfer's, and its size is where the last part ends.
/// </summary>
public sealed record UploadPlan(
    string Fingerprint,
    int FingerprintVersion,
    IReadOnlyList<UploadPlanPart> Parts,
    DateTimeOffset CreatedAt);

/// <summary>One tus upload holding the bytes <c>[Start, Start + Length)</c> of the file.</summary>
public sealed record UploadPlanPart(string UploadId, long Start, long Length);
