
namespace Altinn.Broker.Application.InitializeFileCommand;
public class InitializeFileCommandRequest
{
    public string Consumer { get; set; }
    public string Supplier { get; set; }
    public string Filename { get; set; }
    public string SendersFileReference { get; set; }
    public string SenderExternalId { get; set; }
    public List<string> RecipientExternalIds { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
    public string? Checksum { get; set; }
    public string ClientId { get; set; }
}
