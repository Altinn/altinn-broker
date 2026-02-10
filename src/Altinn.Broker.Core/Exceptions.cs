namespace Altinn.Broker.Core.Exceptions;

public class ServiceOwnerNotConfiguredException : Exception
{
    public ServiceOwnerNotConfiguredException(string serviceOwnerId) : base($"Service owner with id {serviceOwnerId} has not been configured.")
    {
    }
}