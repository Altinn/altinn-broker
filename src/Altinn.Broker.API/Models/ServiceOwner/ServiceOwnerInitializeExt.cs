namespace Altinn.Broker.Models.ServiceOwner;

public class ServiceOwnerInitializeExt
{
    public required string Name { get; set; }
    public required string DeletionTime { get; set; } // ISO8601 Duration
}
