using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Altinn.Broker.Core.Options;

namespace Altinn.Broker.Integrations.Slack;

public class SlackNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _slackWebhookUrl;
    private readonly ILogger<SlackNotificationService> _logger;

    public SlackNotificationService(IConfiguration configuration, ILogger<SlackNotificationService> logger)
    {
        _httpClient = new HttpClient();
        var generalSettings = new GeneralSettings();
        configuration.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
        _slackWebhookUrl = generalSettings.SlackUrl;
        _logger = logger;
    }

    public async Task SendSlackMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(_slackWebhookUrl))
        {
            _logger.LogError("Slack Webhook URL is missing.");
            return;
        }

        try
        {
            var payload = new { text = message };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(_slackWebhookUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Slack API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to send Slack message: {ex.Message}");
        }
    }
} 