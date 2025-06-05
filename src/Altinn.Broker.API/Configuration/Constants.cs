using Altinn.Broker.Common.Constants;

namespace Altinn.Broker.API.Configuration;

public static class Constants
{
    public const string OrgNumberPattern = @"^(?:0192:|" + UrnConstants.OrganizationNumberAttribute + @":)\d{9}$";
}
