
using System.Text.Json;

namespace Altinn.Broker.Integrations.Altinn.Events.Helpers;
internal class LowerCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return name.ToLower();
    }
}
