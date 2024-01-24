using System.Net.Http.Headers;
using System.Text.Json;

using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;
public class ResourceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _resourceOwnerClient;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _unregisteredClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public ResourceControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _resourceOwnerClient = factory.CreateClient();
        _resourceOwnerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_RESOURCEOWNER_TOKEN);
        _senderClient = factory.CreateClient();
        _senderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_SENDER_TOKEN);

        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public void Authentication_CorrectToken_Success()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "broker/api/v1/resource");

        // Act
        var response = _resourceOwnerClient.SendAsync(request).Result;

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Authentication_NonAdminToken_Failure()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "broker/api/v1/resource");

        // Act
        var response = _senderClient.SendAsync(request).Result;

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidResource_CreatedSuccessfully()
    {
        // Arrange
        var resource = ResourceInitializeExtTestFactory.BasicResource();

        // Act
        var initializeResourceResponse = await _resourceOwnerClient.PostAsJsonAsync("broker/api/v1/resource", resource);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, initializeResourceResponse.StatusCode);
    }
}
