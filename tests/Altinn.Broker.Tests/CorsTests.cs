using System.Net;

using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;

public class CorsTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string FileTransferPath = "broker/api/v1/filetransfer";
    private const string AppOrigin = "https://ttd.apps.tt02.altinn.no";
    private readonly HttpClient _client;

    public CorsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
    }

    [Theory]
    [InlineData("https://ttd.apps.tt02.altinn.no")]
    [InlineData("https://digdir.apps.tt02.altinn.no")]
    public async Task Preflight_FromConfiguredAppOrigin_IsAllowedWithCredentials(string origin)
    {
        var response = await _client.SendAsync(CreatePreflight(origin, "POST"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, GetHeader(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", GetHeader(response, "Access-Control-Allow-Credentials"));
        Assert.Contains("POST", GetHeader(response, "Access-Control-Allow-Methods"));
    }

    [Theory]
    [InlineData("https://ttd.apps.altinn.no")]
    [InlineData("https://ab.tt02.altinn.no")]
    [InlineData("https://altinn.no")]
    [InlineData("https://apps.tt02.altinn.no")]
    [InlineData("https://ttd.apps.tt02.altinn.no.example.com")]
    [InlineData("http://ttd.apps.tt02.altinn.no")]
    public async Task Preflight_FromOtherOrigin_IsNotAllowed(string origin)
    {
        var response = await _client.SendAsync(CreatePreflight(origin, "POST"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_ForTusPatch_AllowsTusRequestHeaders()
    {
        var request = CreatePreflight(AppOrigin, "PATCH", $"{FileTransferPath}/upload/tus/{Guid.NewGuid()}");
        request.Headers.Add("Access-Control-Request-Headers", "tus-resumable,upload-offset,x-requested-with");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var allowedHeaders = GetHeader(response, "Access-Control-Allow-Headers");
        Assert.Contains("upload-offset", allowedHeaders);
        Assert.Contains("x-requested-with", allowedHeaders);
    }

    [Fact]
    public async Task Request_FromConfiguredAppOrigin_ExposesTusAndDownloadHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{FileTransferPath}/{Guid.NewGuid()}");
        request.Headers.Add("Origin", AppOrigin);

        var response = await _client.SendAsync(request);

        Assert.Equal(AppOrigin, GetHeader(response, "Access-Control-Allow-Origin"));
        var exposedHeaders = GetHeader(response, "Access-Control-Expose-Headers");
        Assert.Contains("Location", exposedHeaders);
        Assert.Contains("Upload-Offset", exposedHeaders);
        Assert.Contains("Content-Disposition", exposedHeaders);
        Assert.Contains("Content-Range", exposedHeaders);
    }

    private static HttpRequestMessage CreatePreflight(string origin, string method, string path = FileTransferPath)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        return request;
    }

    private static string GetHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : string.Empty;
}
