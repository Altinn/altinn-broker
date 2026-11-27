namespace Altinn.Broker.Core.Services;

/// <summary>
/// True when the production Broker forwarded this request after its own publish check.
/// </summary>
public interface IProductionConfigurationForwardProof
{
    bool IsVerified { get; }
}
