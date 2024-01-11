using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Hangfire.Common;
using Hangfire.States;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;
public class LegacyFileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _legacyClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public LegacyFileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _legacyClient = factory.CreateClient();
        _legacyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_LEGACY_TOKEN);

        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

}
