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
        InitiateBrokerShipmentRequestExt input = new()
        {
            SendersReference = "SendRef_1",
            ServiceCode = "1",
            ServiceEditionCode = 2023,
            Recipients = new List<String> { "911911911" },
            Properties = new Dictionary<string, string>
            {
                { "AA","11" },
                { "BB","22" }
            }
        };

        BrokerShipmentMetadata expected = new()
        {
            SendersReference = "SendRef_1",
            ServiceCode = "1",
            ServiceEditionCode = 2023,
            Recipients = new List<String> { "911911911" },
            Properties = new Dictionary<string, string>
            {
                { "AA","11" },
                { "BB","22" }
            },
            FileList = new List<BrokerFileMetadata>(),
            Status =  Core.Enums.BrokerShipmentStatus.Initialized
        };

        // Act
        var actual = input.MapToBrokerShipment();

        // Assert
        Assert.Equivalent(expected, actual);

    }

}
