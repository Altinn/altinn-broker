using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Shipment metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class BrokerShipmentStatusOverviewExt : BrokerShipmentStatusOverview
    {
        public new List<BrokerFileStatusOverviewExt> FileList {get;set;} = new List<BrokerFileStatusOverviewExt>();
    }
}