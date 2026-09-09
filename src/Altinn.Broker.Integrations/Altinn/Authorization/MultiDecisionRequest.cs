using Altinn.Authorization.ABAC.Xacml.JsonProfile;

namespace Altinn.Broker.Integrations.Altinn.Authorization;

internal sealed record MultiDecisionKey(string ResourceId, string Action);

/// <summary>
/// A multi-decision request and the decisions it asks for, in the order they were requested.
/// </summary>
internal sealed record MultiDecisionRequest(XacmlJsonRequestRoot Request, IReadOnlyList<MultiDecisionKey> Decisions);
