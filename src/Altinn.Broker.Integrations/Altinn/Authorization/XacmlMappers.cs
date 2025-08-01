using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.Json;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Common;
using Altinn.Broker.Common.Constants;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Broker.Common.Helpers.Models;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Broker.Integrations.Altinn.Authorization;

/// <summary>
/// Utility class for converting Events to XACML request
/// </summary>
public static class XacmlMappers
{
    /// <summary>
    /// Default issuer for attributes
    /// </summary>
    internal const string DefaultIssuer = "Altinn";

    /// <summary>
    /// Default type for attributes
    /// </summary>
    internal const string DefaultType = "string";

    /// <summary>
    /// Subject id for multi requests. Inde should be appended.
    /// </summary>
    internal const string SubjectId = "s";

    /// <summary>
    /// Action id for multi requests. Inde should be appended.
    /// </summary>
    internal const string ActionId = "a";

    /// <summary>
    /// Resource id for multi requests. Inde should be appended.
    /// </summary>
    internal const string ResourceId = "r";

    /// <param name="actionType">Action type represented as a string</param>
    /// <param name="includeResult">A value indicating whether the value should be included in the result</param>
    /// <returns>A XacmlJsonCategory</returns>
    internal static XacmlJsonCategory CreateActionCategory(string actionType, bool includeResult = false)
    {
        XacmlJsonCategory actionAttributes = new()
        {
            Attribute = new List<XacmlJsonAttribute>
                {
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, actionType, DefaultType, DefaultIssuer, includeResult),
                }
        };
        return actionAttributes;
    }

    /// <summary>
    /// Creates XACML resource category for authorization requests.
    /// If id is required this should be included by the caller. 
    /// Attribute eventId is tagged with `includeInResponse`</remarks>
    internal static XacmlJsonCategory CreateResourceCategory(string resourceId, string party, string? instanceId)
    {
        XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.OrganizationNumberAttribute, party, DefaultType, DefaultIssuer));
        if (instanceId is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceInstance, instanceId, DefaultType, DefaultIssuer));
        }
        return resourceCategory;

    }

    /// <param name="user">ClaimsPrincipal containing Maskinporten token claims</param>
    /// <returns>XacmlJsonCategory with appropriate subject attributes for Maskinporten tokens</returns>
    internal static XacmlJsonCategory CreateMaskinportenSubjectCategory(ClaimsPrincipal user)
    {
        XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
        List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();
        
        // Add organization number attributes using centralized extraction logic
        var organizationNumber = user.GetCallerOrganizationId();
        if (!string.IsNullOrEmpty(organizationNumber))
        {
            list.Add(CreateXacmlJsonAttribute("urn:altinn:organizationnumber", organizationNumber, "string", "altinn"));
            list.Add(CreateXacmlJsonAttribute("urn:altinn:organization:identifier-no", organizationNumber, "string", "altinn"));
        }
        
        foreach (Claim claim in user.Claims)
        {
            if (IsScopeClaim(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute("urn:scope", claim.Value, "string", claim.Issuer));
            }
            else if (IsJtiClaim(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute("urn:altinn:sessionid", claim.Value, "string", claim.Issuer));
            }
            else if (IsValidUrn(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute(claim.Type, claim.Value, "string", claim.Issuer));
            }
        }
        
        xacmlJsonCategory.Attribute = list;
        return xacmlJsonCategory;
    }

    private static bool IsValidUrn(string value)
    {
        Regex regex = new Regex("^urn*");
        return regex.Match(value).Success;
    }

    private static bool IsScopeClaim(string value)
    {
        return value.Equals("scope");
    }

    private static bool IsJtiClaim(string value)
    {
        return value.Equals("jti");
    }

    private static XacmlJsonAttribute CreateXacmlJsonAttribute(string attributeId, string value, string dataType, string issuer, bool includeResult = false)
    {
        XacmlJsonAttribute xacmlJsonAttribute = new XacmlJsonAttribute();
        xacmlJsonAttribute.AttributeId = attributeId;
        xacmlJsonAttribute.Value = value;
        xacmlJsonAttribute.DataType = dataType;
        xacmlJsonAttribute.Issuer = issuer;
        xacmlJsonAttribute.IncludeInResult = includeResult;
        return xacmlJsonAttribute;
    }
}
