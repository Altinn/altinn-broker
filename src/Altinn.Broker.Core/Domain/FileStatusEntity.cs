namespace Altinn.Broker.Core.Domain;
public class FileStatusEntity
{
    public Guid FileId { get; set; }
    public Enums.FileStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
    public string? DetailedStatus { get; set; }
}
