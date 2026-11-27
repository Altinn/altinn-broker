using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Profile;

/// <summary>
/// Reads and updates the end user's language on the Altinn profile API.
/// Broker calls the platform directly. It does not go through Arbeidsflate.
/// </summary>
public class AltinnProfileService : IAltinnProfileService
{
    private const string CurrentUserPath = "profile/api/v1/users/current";
    private const string ProfileSettingsPath = "profile/api/v1/users/current/profilesettings";

    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly HttpClient _httpClient;
    private readonly IEndUserTokenProvider _endUserTokenProvider;
    private readonly ILogger<AltinnProfileService> _logger;

    public AltinnProfileService(
        HttpClient httpClient,
        IOptions<AltinnOptions> altinnOptions,
        IEndUserTokenProvider endUserTokenProvider,
        ILogger<AltinnProfileService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _endUserTokenProvider = endUserTokenProvider;
        _logger = logger;
    }

    public async Task<string?> GetLanguage(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequest(HttpMethod.Get, CurrentUserPath, content: null);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Altinn profile has no user profile for the current end user.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Error when reading the Altinn profile. Status code was {statusCode}, error was {error}",
                response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken));
            throw new HttpRequestException($"Altinn profile responded {(int)response.StatusCode} when asked for the current user");
        }

        UserProfileResponse? profile;
        try
        {
            profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Unexpected json response when reading the Altinn profile");
            throw new HttpRequestException("Altinn profile returned an unexpected response for the current user", exception);
        }

        var language = profile?.ProfileSettingPreference?.Language;
        return string.IsNullOrWhiteSpace(language) ? null : language;
    }

    public async Task UpdateLanguage(string language, CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(new ProfileSettingsUpdate { Language = language }, options: JsonOptions);
        using var request = await CreateRequest(HttpMethod.Patch, ProfileSettingsPath, content);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        _logger.LogError(
            "Error when updating the Altinn profile language. Status code was {statusCode}, error was {error}",
            response.StatusCode,
            await response.Content.ReadAsStringAsync(cancellationToken));
        throw new HttpRequestException($"Altinn profile responded {(int)response.StatusCode} when updating the language");
    }

    private async Task<HttpRequestMessage> CreateRequest(HttpMethod method, string path, HttpContent? content)
    {
        var altinnToken = await _endUserTokenProvider.GetAltinnToken();
        if (string.IsNullOrWhiteSpace(altinnToken))
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated end user request");
        }

        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", altinnToken);
        return request;
    }

    private sealed class UserProfileResponse
    {
        public ProfileSettingPreferenceResponse? ProfileSettingPreference { get; set; }
    }

    private sealed class ProfileSettingPreferenceResponse
    {
        public string? Language { get; set; }
    }

    private sealed class ProfileSettingsUpdate
    {
        public required string Language { get; set; }
    }
}
