using System.Net;

using Altinn.Broker.Application;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

using Xunit;

namespace Altinn.Broker.Tests;

public class SingleStreamLimitWebApplicationFactory : StripedStorageWebApplicationFactory
{
    public const long MaxChunkSizeBytes = 16;
    public const long MaxSingleStreamLength = MaxBlocksPerStripe * MaxChunkSizeBytes;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TusOptions:MaxChunkSizeBytes", MaxChunkSizeBytes.ToString() }
            });
        });
    }
}

public class TusSingleStreamLimitTests(SingleStreamLimitWebApplicationFactory factory)
    : StripedUploadTestBase(factory), IClassFixture<SingleStreamLimitWebApplicationFactory>
{
    [Fact]
    public async Task CreateSingleStreamUpload_LongerThanOneBlobCanHold_IsRejected()
    {
        // Arrange
        var fileTransferId = await InitializeFileTransfer();

        // Act
        var response = await CreateSingleStreamUpload(fileTransferId, SingleStreamLimitWebApplicationFactory.MaxSingleStreamLength + 1);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(Errors.PartialUploadTooLong.Message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateSingleStreamUpload_AtTheLimit_IsAccepted()
    {
        // Arrange
        var fileTransferId = await InitializeFileTransfer();

        // Act
        var response = await CreateSingleStreamUpload(fileTransferId, SingleStreamLimitWebApplicationFactory.MaxSingleStreamLength);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreatePartial_LongerThanTheSingleStreamLimit_IsAccepted()
    {
        // Arrange
        var fileTransferId = await InitializeFileTransfer();

        // Act & Assert
        await CreatePartial(fileTransferId, (int)SingleStreamLimitWebApplicationFactory.MaxSingleStreamLength + 1);
    }

    private async Task<HttpResponseMessage> CreateSingleStreamUpload(Guid fileTransferId, long length)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"broker/api/v1/filetransfer/upload/tus/{fileTransferId}");
        request.Headers.Add("Tus-Resumable", "1.0.0");
        request.Headers.Add("Upload-Length", length.ToString());
        return await _senderClient.SendAsync(request);
    }
}
