namespace Altinn.Broker.Core.Domain.Enums;
public enum ResourceAccessLevel
{
    Read,
    Write,
    /// <summary>Right to configure/publish a broker resource (e.g. resource settings).</summary>
    Publish
}
