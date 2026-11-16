using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Configuration;

/// <summary>
/// Forwards resource configuration reads and updates to the TT02 Broker API.
/// The caller's production Altinn token is sent as a bearer token; the browser
/// session cookie stays on this host.
/// </summary>
public sealed class StagingResourceConfigurationProxy
{
    public const string Tt02Environment = "tt02";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IEndUserTokenProvider _endUserTokenProvider;
    private readonly string _forwardKey;
    private readonly ILogger<StagingResourceConfigurationProxy> _logger;

    public StagingResourceConfigurationProxy(
        HttpClient httpClient,
        IEndUserTokenProvider endUserTokenProvider,
        IOptions<AltinnOptions> altinnOptions,
        IHostEnvironment hostEnvironment,
        ILogger<StagingResourceConfigurationProxy> logger)
    {
        _httpClient = httpClient;
        _endUserTokenProvider = endUserTokenProvider;
        _forwardKey = altinnOptions.Value.StagingConfigurationForwardKey.Trim();
        _logger = logger;
        IsEnabled = hostEnvironment.IsProduction()
            && _httpClient.BaseAddress is not null
            && altinnOptions.Value.HasStagingConfigurationForward();
    }

    public bool IsEnabled { get; }

    public static bool IsTt02(string? environment)
        => string.Equals(environment, Tt02Environment, StringComparison.OrdinalIgnoreCase);

    public async Task<string?> ResolveAccessTokenAsync(HttpContext httpContext)
    {
        var sessionToken = await _endUserTokenProvider.GetAltinnToken();
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            return sessionToken;
        }

        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var bearer = authorization[bearerPrefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(bearer) ? null : bearer;
        }

        return null;
    }

    public Task<ContentResult> ForwardAsync(
        string method,
        string resourceId,
        string? onBehalfOf,
        ResourceExt? body,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var path = $"broker/api/v1/resource/{Uri.EscapeDataString(resourceId)}";
        if (!string.IsNullOrWhiteSpace(onBehalfOf))
        {
            path += $"?onBehalfOf={Uri.EscapeDataString(onBehalfOf)}";
        }

        return SendAsync(new HttpMethod(method), path, body, accessToken, cancellationToken);
    }

    /// <summary>
    /// Reads TT02's access-list candidates. The query is not forwarded with
    /// <c>environment</c>, so TT02 resolves the list locally.
    /// </summary>
    public Task<ContentResult> ForwardAllowedRecipientsAsync(
        string resourceId,
        string party,
        bool ignoreRequiredParty,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var path =
            $"broker/api/v1/resource/{Uri.EscapeDataString(resourceId)}/allowed-recipients?party={Uri.EscapeDataString(party)}";
        if (ignoreRequiredParty)
        {
            path += "&ignoreRequiredParty=true";
        }

        return SendAsync(HttpMethod.Get, path, body: null, accessToken, cancellationToken);
    }

    private async Task<ContentResult> SendAsync(
        HttpMethod method,
        string path,
        ResourceExt? body,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (_forwardKey.Length > 0)
        {
            request.Headers.TryAddWithoutValidation(ProductionConfigurationForwardProof.HeaderName, _forwardKey);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseBody,
                ContentType = string.IsNullOrWhiteSpace(responseBody)
                    ? null
                    : contentType ?? "application/json"
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(exception, "Could not reach the TT02 Broker API to configure a resource.");
            return new ContentResult
            {
                StatusCode = StatusCodes.Status502BadGateway,
                Content = """{"detail":"Could not reach TT02 to read or update the resource configuration."}""",
                ContentType = "application/json"
            };
        }
    }
}
