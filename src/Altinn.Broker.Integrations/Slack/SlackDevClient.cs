using Slack.Webhooks;

namespace Altinn.Correspondence.Integrations.Slack
{
    public class SlackDevClient : ISlackClient
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _webhookUri;
        private const string POST_SUCCESS = "ok";
        public SlackDevClient(string webhookUrl, HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out _webhookUri))
                // throw new ArgumentException("Please enter a valid webhook url"); Commented out from source code to avoid throwing exception when testing without providing a webhook url
                return;
        }
        public virtual bool Post(SlackMessage slackMessage)
        {
            return PostAsync(slackMessage, false).Result;
        }
        public async Task<bool> PostAsync(SlackMessage slackMessage)
        {
            return await PostAsync(slackMessage, true);
        }
        public async Task<bool> PostAsync(SlackMessage slackMessage, bool configureAwait = true)
        {
            if (_webhookUri == null) return true; // Mock success if no webhook url is provided

            using (var request = new HttpRequestMessage(HttpMethod.Post, _webhookUri))
            {
                request.Content = new StringContent(slackMessage.AsJson(), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request).ConfigureAwait(configureAwait);
                var content = await response.Content.ReadAsStringAsync();
                return content.Equals(POST_SUCCESS, StringComparison.OrdinalIgnoreCase);
            }
        }
        public bool PostToChannels(SlackMessage message, IEnumerable<string> channels)
        {
            return true;
        }
        public IEnumerable<Task<bool>> PostToChannelsAsync(SlackMessage message, IEnumerable<string> channels)
        {
            return [];
        }
    }
}