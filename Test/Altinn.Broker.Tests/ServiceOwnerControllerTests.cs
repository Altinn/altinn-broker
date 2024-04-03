using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;

using Altinn.Broker.Models.ServiceOwner;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;
public class ServiceOwnerControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _serviceOwnerClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public ServiceOwnerControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _serviceOwnerClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task Get_ServiceOwner()
    {
        var response = await _serviceOwnerClient.GetFromJsonAsync<ServiceOwnerOverviewExt>($"broker/api/v1/serviceowner", _responseSerializerOptions);
        Assert.Equal("Digitaliseringsdirektoratet Avd Oslo", response.Name);
    }

    [Fact]
    public async Task Update_FileRetention_For_ServiceOwner()
    {
        var serviceOwner = await _serviceOwnerClient.GetFromJsonAsync<ServiceOwnerOverviewExt>($"broker/api/v1/serviceowner", _responseSerializerOptions);
        Assert.NotNull(serviceOwner);
        var ttl = serviceOwner.FileTransferTimeToLive.Add(TimeSpan.FromDays(1));

        var serviceOwnerUpdateFileRetentionExt = new ServiceOwnerUpdateFileRetentionExt
        {
            FileTransferTimeToLive = XmlConvert.ToString(ttl)
        };

        var retentionResponse = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/serviceowner/fileretention", serviceOwnerUpdateFileRetentionExt);
        Assert.True(HttpStatusCode.OK == retentionResponse.StatusCode);
        var response = await _serviceOwnerClient.GetFromJsonAsync<ServiceOwnerOverviewExt>($"broker/api/v1/serviceowner", _responseSerializerOptions);
        Assert.NotNull(response);
        Assert.Equal(ttl, response.FileTransferTimeToLive);
    }


}
