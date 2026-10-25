using System.Net;
using System.Net.Http.Json;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;

/// <summary>
/// The condition endpoint is called by Altinn Notification, so the route, the scope it is gated on and the exact
/// response body are all contract towards them.
/// </summary>
public class NotificationConditionEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationConditionEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string ConditionEndpoint(Guid notificationId) => $"broker/api/v1/notification/{notificationId}/check";

    [Fact]
    public async Task CheckNotificationCondition_WithoutToken_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync(ConditionEndpoint(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CheckNotificationCondition_WithoutTheConditionCheckScope_IsForbidden()
    {
        var client = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);

        var response = await client.GetAsync(ConditionEndpoint(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CheckNotificationCondition_ForUnknownNotification_AnswersThatItShouldNotBeSent()
    {
        var token = TestTokenHelper.CreateMaskinportenToken("991825827", AuthorizationConstants.NotificationCheckScope);
        var client = _factory.CreateClientWithAuthorization(token);

        var response = await client.GetAsync(ConditionEndpoint(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("{\"sendNotification\":false}", body);
    }
}
