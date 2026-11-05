namespace Altinn.Broker.Application.UploadPlans;

public class SaveUploadPlanRequest
{
    public Guid FileTransferId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int FingerprintVersion { get; set; }
    public List<UploadPlanPartRequest> Parts { get; set; } = [];
}

public class UploadPlanPartRequest
{
    public string UploadId { get; set; } = string.Empty;
    public long Start { get; set; }
    public long Length { get; set; }
}
