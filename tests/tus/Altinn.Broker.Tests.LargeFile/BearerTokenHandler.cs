using System.Net.Http.Headers;

namespace Altinn.Broker.Tests.LargeFile;

public sealed class BearerTokenHandler(string token) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
