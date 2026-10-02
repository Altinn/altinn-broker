using System.Net.Http.Json;
using Altinn.Broker.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Models.Notifications;
using System.Text.Json;
using System.Net;

namespace Altinn.Broker.Integrations.Altinn.Notifications;

public class AltinnNotificationService : IAltinnNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnNotificationService> _logger;

    public AltinnNotificationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<NotificationOrderResponseV2?> CreateNotificationV2(NotificationOrderRequestV2 notificationRequest, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating notification in Altinn Notification v2");
        var response = await _httpClient.PostAsJsonAsync("notifications/api/v1/future/orders", notificationRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create notification in Altinn Notification v2. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", responseJson);

            if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
            {
                throw new BadHttpRequestException($"Failed to create notification V2. Status code: {response.StatusCode}. Response body: {responseJson}. IdempotencyId: {notificationRequest.IdempotencyId}");
            }

            return null;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<NotificationOrderResponseV2>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null json response from Notification v2. Response body: {Response}. IdempotencyId: {IdempotencyId}", responseJson, notificationRequest.IdempotencyId);
            throw new JsonException($"Failed to deserialize notification V2 response - received null. Response body: {responseJson}. IdempotencyId: {notificationRequest.IdempotencyId}");
        }
        return responseContent;
    }

    public async Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsync($"notifications/api/v1/orders/{orderId}/cancel", null, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Could not cancel notification with orderId: " + orderId);
            return false;
        }
        return true;
    }

    public async Task<NotificationStatusResponseV2> GetNotificationDetailsV2(string shipmentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"notifications/api/v1/future/shipment/{shipmentId}", cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get details about notification v2 from Altinn Notification. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
            throw new BadHttpRequestException("Failed to process response from Altinn Notification");
        }
        var responseContent = await response.Content.ReadFromJsonAsync<NotificationStatusResponseV2>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Notification v2.");
            throw new BadHttpRequestException("Failed to process get notification details v2 from Altinn Notification");
        }
        return responseContent;
    }
}