using System.Diagnostics.CodeAnalysis;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Repositories.Interfaces
{    
    public interface IFileStorage
    {
        Task<BrokerFileStatusOverview> SaveFile(Guid shipmentId, Stream filestream, BrokerFileInitalize brokerFile);
    }    
}