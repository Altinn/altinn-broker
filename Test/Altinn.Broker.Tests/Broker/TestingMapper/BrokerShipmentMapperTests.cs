using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Mappers;
using Altinn.Broker.Models;

using Xunit;

namespace Altinn.Broker.Tests.Broker.TestingMapper;
public class BrokerShipmentMapperTests
{
    [Fact]
    public void MapToMapToBrokerShipment_SimpleCase()
    {
        // Arrange
        BrokerShipmentInitializeExt input = new()
        {
            BrokerResourceId = Guid.NewGuid(),
            Recipients = new List<string> { "911911911" },
            Metadata = new Dictionary<string, string>
            {
                { "AA","11" },
                { "BB","22" }
            },
            Files = new List<FileInitalizeExt>(),
            SendersShipmentReference = "SendRef_1",
            Sender = "9090908"
        };

        BrokerShipmentInitialize expected = new()
        {
            SendersShipmentReference = "SendRef_1",
            BrokerResourceId = input.BrokerResourceId,
            Recipients = new List<String> { "911911911" },
            Metadata = new Dictionary<string, string>
            {
                { "AA","11" },
                { "BB","22" }
            },
            Files = new List<BrokerFileInitalize>(),            
            Sender = "9090908"
        };

        // Act
        var actual = input.MapToInternal();

        // Assert
        Assert.Equivalent(expected, actual);

    }

}
