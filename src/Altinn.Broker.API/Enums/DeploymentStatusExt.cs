/// <summary>
/// In the context of Azure deployment, "Prepared" corresponds to resource group deployed and "Ready" corresponds to all resources ready
/// </summary>
public enum DeploymentStatusExt
{
    NotStarted,
    DeployingResources,
    Ready
}
