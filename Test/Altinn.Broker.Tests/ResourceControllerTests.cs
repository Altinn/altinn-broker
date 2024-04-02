using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.API.Models;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;
public class ResourceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _serviceOwnerClient;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public ResourceControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _serviceOwnerClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);

        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

    }

    [Fact]
    public async Task Update_Resource_Max_Upload_Size()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync<ResourceExt>($"broker/api/v1/resource/altinn-broker-test-resource-1", new ResourceExt
        {
            MaxFileTransferSize = 99999
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_Max_Upload_Size_Over_Global_Should_Fail()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync<ResourceExt>($"broker/api/v1/resource/altinn-broker-test-resource-1", new ResourceExt
        {
            MaxFileTransferSize = 999999999999999
        });
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Resource_Should_Fail_For_Sender()
    {
        var response = await _senderClient.PutAsJsonAsync<ResourceExt>($"broker/api/v1/resource/altinn-broker-test-resource-1", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Resource_Should_Fail_For_Receiver()
    {
        var response = await _recipientClient.PutAsJsonAsync<ResourceExt>($"broker/api/v1/resource/altinn-broker-test-resource-1", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Resource_file_transfer_time_to_live()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync<ResourceExt>($"broker/api/v1/resource/altinn-broker-test-resource-1", new ResourceExt
        {
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_file_transfer_time_to_live_Over_Limit_Should_Fail()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync<ResourceExt>($"broker/api/v1/resource/altinn-broker-test-resource-1", new ResourceExt
        {
            FileTransferTimeToLive = "P366D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }
}
