using Altinn.Broker.Core.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Core.Models
{
    public class BrokerFileStatusDetailsExt : BrokerFileStatusOverviewExt
    {
        public List<BrokerFileStatusEventExt> FileStatusHistory { get; set; } = new List<BrokerFileStatusEventExt>();
    }
}
