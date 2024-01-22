namespace Altinn.Broker.Models.ResourceOwner;

public class ResourceOwnerOverviewExt
{
    public ResourceOwnerOverviewExt() { }

    public string Name { get; set; }

    public DeploymentStatusExt DeploymentStatus { get; set; }

    public TimeSpan FileTimeToLive { get; set; }
}
