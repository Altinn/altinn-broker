namespace Altinn.Broker.Tests.LargeFile;

public sealed class BearerTokenHandler(AccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        tokenProvider.ApplyAuthorization(request, token);
        return await base.SendAsync(request, cancellationToken);
    }
}
