namespace Altinn.Broker.Core.Domain.Enums;
public enum ResourceAccessLevel
{
    Read,
    Write,
    /// <summary>Right to configure a broker resource (the <c>access</c> action).</summary>
    Access
}
