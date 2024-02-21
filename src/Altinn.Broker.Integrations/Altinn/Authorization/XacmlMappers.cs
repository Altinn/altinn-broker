using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Core.Domain;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;

using Azure.Messaging;

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

    /// <summary>
    /// Creates an action category with the provided action type as an attribute
    /// </summary>
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
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, "publish", DefaultType, DefaultIssuer, includeResult) // Permission to publish events is always required
                }
        };
        return actionAttributes;
    }

    /// <summary>
    /// Creates a resource category for resource.
    /// </summary>
    /// <remarks>
    /// If id is required this should be included by the caller. 
    /// Attribute eventId is tagged with `includeInResponse`</remarks>
    internal static XacmlJsonCategory CreateResourceCategory(ResourceEntity resourceEntity)
    {
        XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceEntity.Id, DefaultType, DefaultIssuer));
        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, "altinn-broker", DefaultType, DefaultIssuer));

        return resourceCategory;
    }
}
