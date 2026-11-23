using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetEligibleParties;

public class GetEligiblePartiesResponse
{
    public List<string> EligibleParties { get; set; } = new List<string>();
}