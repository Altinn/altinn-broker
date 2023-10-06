using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerFileStatusDetails : BrokerFileStatusOverview
    {
        public List<FileStatusEvent> FileStatusHistory{get;set;} = new List<FileStatusEvent>();
    }
}