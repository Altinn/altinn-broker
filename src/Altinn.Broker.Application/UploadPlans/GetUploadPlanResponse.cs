namespace Altinn.Broker.Application.UploadPlans;

public class GetUploadPlanResponse
{
    public required string Fingerprint { get; set; }
    public required int FingerprintVersion { get; set; }
    public required long UploadedBytes { get; set; }
    public required List<GetUploadPlanPartResponse> Parts { get; set; }
}

public class GetUploadPlanPartResponse
{
    public required string UploadId { get; set; }
    public required long Start { get; set; }
    public required long Length { get; set; }
    public required long UploadedBytes { get; set; }
}
