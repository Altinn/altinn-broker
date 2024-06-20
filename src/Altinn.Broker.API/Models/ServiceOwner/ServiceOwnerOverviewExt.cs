namespace Altinn.Broker.Models.ServiceOwner;

public class ServiceOwnerOverviewExt
{
    public ServiceOwnerOverviewExt() { }

    public required string Name { get; set; }

    public required DeploymentStatusExt DeploymentStatus { get; set; }
}
