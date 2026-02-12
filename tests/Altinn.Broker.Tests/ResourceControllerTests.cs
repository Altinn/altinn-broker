using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Application;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace Altinn.Broker.Tests;
public class ResourceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _serviceOwnerClient;
    private readonly HttpClient _serviceOwnerClientNotConfigured;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public ResourceControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _serviceOwnerClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _serviceOwnerClientNotConfigured = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN_NOT_CONFIGURED);
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
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            MaxFileTransferSize = 99999
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_Max_Upload_Size_Over_Global_Should_Fail()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            MaxFileTransferSize = 999999999999999
        });
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Resource_Should_Fail_For_Sender()
    {
        var response = await _senderClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Resource_Should_Fail_For_Receiver()
    {
        var response = await _recipientClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Resource_file_transfer_time_to_live()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_file_transfer_time_to_live_Over_Limit_Should_Fail()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            FileTransferTimeToLive = "P366D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_purge_file_transfer_grace_period()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferGracePeriod = "PT2H"
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_purge_file_transfer_grace_period_Over_Limit_Should_Fail()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferGracePeriod = "PT25H"
        });
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var response2 = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferGracePeriod = "P3D"
        });
        Assert.True(response2.StatusCode == HttpStatusCode.BadRequest, await response2.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Update_Resource_purge_file_transfer_after_all_recipients_confirmed()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferAfterAllRecipientsConfirmed = true
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var response2 = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferAfterAllRecipientsConfirmed = false
        });
        Assert.True(response2.IsSuccessStatusCode, await response2.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetResource_WithResourceNotConfigured_ReturnsBadRequest()
    {
        var response = await _serviceOwnerClient.GetAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_NOT_CONFIGURED}");
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        
        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.ResourceHasNotBeenConfigured.Message, parsedError.Detail);
    }

    [Fact]
    public async Task GetResource_WithoutPermissions_ReturnsUnauthorized()
    {
        var response = await _serviceOwnerClientNotConfigured.GetAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ConfigureResource_WithoutServiceOwnerConfiguration_ReturnsBadRequest()
    {
        var response = await _serviceOwnerClientNotConfigured.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_WITH_UNCONFIGURED_SERVICEOWNER}", new ResourceExt
        {
            UseManifestFileShim = false
        });
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        
        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.ServiceOwnerHasNotBeenConfigured.Message, parsedError.Detail);
    }

    [Fact]
    public async Task ConfigureResource_WithUnConfiguredResourceOfDifferentServiceOwner_ReturnsUnauthorized()
    {
        var response = await _serviceOwnerClientNotConfigured.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_WITH_CONFIGURED_SERVICEOWNER}", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.NoAccessToResource.Message, parsedError.Detail);
    }

    [Fact]
    public async Task ConfigureResource_WithConfiguredResourceOfDifferentServiceOwner_ReturnsUnauthorized()
    {
        var response = await _serviceOwnerClientNotConfigured.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.NoAccessToResource.Message, parsedError.Detail);
    }

    [Fact]
    public async Task ConfigureResource_WithInvalidResourceDefinition_ReturnsForbidden()
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/invalidresource", new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P30D"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.InvalidResourceDefinition.Message, parsedError.Detail);
    }
}
