namespace Altinn.Broker.Application.GetActiveFileTransferDetails;

public class GetActiveFileTransferDetailsResponse
{
    public Guid? FileTransferId { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? SendersFileTransferReference { get; set; }
    public string? FileName { get; set; }
    public string? FileTransferSize { get; set; }
    // public string? Status { get; set; }
    public string? Created { get; set; }
    public bool? UseVirusScan { get; set; }
    public string? Published { get; set; }
    public string? Sender { get; set; }
    public string? SenderName { get; set; }
    public  List<RecipientDetails>? Recipients { get; set; }
    public Dictionary<string, string>? PropertyList { get; set; }
    public string? ServiceOwner { get; set; }
    public string? ExpirationTime { get; set; }
    public string? Status { get; set; }
    public bool? IsSender { get; set; }
    public string? ActorDownloadStatus { get; set; }
}

public class RecipientDetails
{
    public required string Recipient { get; set; }
    public string? RecipientName { get; set; }
}