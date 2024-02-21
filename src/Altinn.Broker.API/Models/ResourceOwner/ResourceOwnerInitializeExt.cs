using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Models.ResourceOwner;

public class ResourceOwnerInitializeExt
{
    public string Name { get; set; }
    public string DeletionTime { get; set; } // ISO8601 Duration
}
