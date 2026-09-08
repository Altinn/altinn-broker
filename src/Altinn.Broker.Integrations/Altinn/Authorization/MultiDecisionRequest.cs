using Altinn.Authorization.ABAC.Xacml.JsonProfile;

namespace Altinn.Broker.Integrations.Altinn.Authorization;

/// <summary>
/// One decision asked for in a multi-decision request.
/// </summary>
internal sealed record MultiDecisionKey(string ResourceId, string Action);

/// <summary>
/// A multi-decision request and the decisions it asks for, in the order they were requested.
/// </summary>
internal sealed record MultiDecisionRequest(XacmlJsonRequestRoot Request, IReadOnlyList<MultiDecisionKey> Decisions);
