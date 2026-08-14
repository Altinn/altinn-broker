using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Broker.API.Models;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Enums;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Broker.Tests;

public class StripedStorageWebApplicationFactory : CustomWebApplicationFactory
{
    public const long StripeSize = 16;
    public const int MaxBlocksPerStripe = 8;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AzureStorageOptions:StripeSizeBytes", StripeSize.ToString() },
                { "AzureStorageOptions:MaxBlocksPerStripe", MaxBlocksPerStripe.ToString() }
            });
        });
    }
}

public abstract class StripedUploadTestBase
{
    protected readonly StripedStorageWebApplicationFactory _factory;
    protected readonly HttpClient _senderClient;
    protected readonly HttpClient _recipientClient;
    protected readonly JsonSerializerOptions _responseSerializerOptions;

    protected StripedUploadTestBase(StripedStorageWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
        _responseSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _responseSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    protected static BlobContainerClient GetContainerClient() =>
        new BlobServiceClient(AzureConstants.AzuriteUrl).GetBlobContainerClient("brokerfiles");

    protected static async Task<List<string>> ListBlobs(BlobContainerClient containerClient, Guid fileTransferId)
    {
        var names = new List<string>();
        await foreach (var blob in containerClient.GetBlobsAsync(
            BlobTraits.None,
            BlobStates.None,
            StripeLayout.GetStripeBlobPrefix(fileTransferId),
            CancellationToken.None))
        {
            names.Add(blob.Name);
        }

        return names;
    }

    protected async Task<Guid> InitializeFileTransfer()
    {
        var response = await _senderClient.PostAsJsonAsync(
            "broker/api/v1/filetransfer",
            FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var result = await response.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>(_responseSerializerOptions);
        Assert.NotNull(result);
        return result!.FileTransferId;
    }

    protected async Task<Uri> CreatePartial(Guid fileTransferId, int length)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"broker/api/v1/filetransfer/upload/tus/{fileTransferId}");
        request.Headers.Add("Tus-Resumable", "1.0.0");
        request.Headers.Add("Upload-Length", length.ToString());
        request.Headers.Add("Upload-Concat", "partial");
        var response = await _senderClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return response.Headers.Location!;
    }

    protected async Task PatchInChunks(Uri uploadUrl, byte[] content, int chunkSize)
    {
        var offset = 0;
        while (offset < content.Length)
        {
            var length = Math.Min(chunkSize, content.Length - offset);
            var request = new HttpRequestMessage(HttpMethod.Patch, uploadUrl)
            {
                Content = new ByteArrayContent(content, offset, length)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
            request.Headers.Add("Tus-Resumable", "1.0.0");
            request.Headers.Add("Upload-Offset", offset.ToString());

            var response = await _senderClient.SendAsync(request);
            Assert.True(
                response.StatusCode == HttpStatusCode.NoContent,
                $"PATCH at offset {offset} returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            offset += length;
        }
    }

    protected async Task ConcatenatePartials(Guid fileTransferId, IEnumerable<Uri> locations)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"broker/api/v1/filetransfer/upload/tus/{fileTransferId}");
        request.Headers.Add("Tus-Resumable", "1.0.0");
        request.Headers.Add("Upload-Concat", $"final;{string.Join(' ', locations.Select(location => location.ToString()))}");
        var response = await _senderClient.SendAsync(request);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Final concat returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    protected static byte[] CreateContent(int length)
    {
        var content = new byte[length];
        for (var i = 0; i < length; i++)
        {
            content[i] = (byte)('a' + (i % 26));
        }

        return content;
    }

    protected async Task<FileTransferOverviewExt> WaitForStatus(Guid fileTransferId, FileTransferStatusExt status)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var overview = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>(
                $"broker/api/v1/filetransfer/{fileTransferId}",
                _responseSerializerOptions);
            Assert.NotNull(overview);
            if (overview!.FileTransferStatus == status)
            {
                return overview;
            }

            Assert.NotEqual(FileTransferStatusExt.Failed, overview.FileTransferStatus);
            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"File transfer {fileTransferId} never reached {status}");
    }

    protected async Task<Guid> UploadStripedFile(byte[] content, int chunkSize = 7)
    {
        var fileTransferId = await InitializeFileTransfer();
        var location = await CreatePartial(fileTransferId, content.Length);
        await PatchInChunks(location, content, chunkSize);
        await ConcatenatePartials(fileTransferId, [location]);
        await WaitForStatus(fileTransferId, FileTransferStatusExt.Published);
        return fileTransferId;
    }
}

public class TusStripedUploadTests(StripedStorageWebApplicationFactory factory)
    : StripedUploadTestBase(factory), IClassFixture<StripedStorageWebApplicationFactory>
{
    [Fact]
    public async Task StripedConcatenation_SpreadsContentOverStripesAndDownloadsIntact()
    {
        // Arrange
        var fileTransferId = await InitializeFileTransfer();
        var partialContents = Enumerable.Range(0, 4).Select(_ => CreateContent(25)).ToList();
        var locations = new List<Uri>();
        foreach (var partialContent in partialContents)
        {
            locations.Add(await CreatePartial(fileTransferId, partialContent.Length));
        }

        var expected = partialContents.SelectMany(content => content).ToArray();
        var layout = new StripeLayout(expected.Length, StripedStorageWebApplicationFactory.StripeSize);
        Assert.True(layout.StripeCount > 1, "The test payload should not fit in a single stripe");

        // Act
        for (var i = 0; i < locations.Count; i++)
        {
            await PatchInChunks(locations[i], partialContents[i], chunkSize: 7);
        }

        await ConcatenatePartials(fileTransferId, locations);
        var overview = await WaitForStatus(fileTransferId, FileTransferStatusExt.Published);
        var download = await _recipientClient.GetByteArrayAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");

        // Assert
        Assert.Equal(expected.Length, overview.FileTransferSize);
        Assert.Equal(expected, download);

        var containerClient = GetContainerClient();
        for (var stripeIndex = 0; stripeIndex < layout.StripeCount; stripeIndex++)
        {
            var blobClient = containerClient.GetBlobClient(StripeLayout.GetStripeBlobName(fileTransferId, stripeIndex));
            Assert.True(await blobClient.ExistsAsync(), $"Stripe {stripeIndex} was not committed");
            var properties = await blobClient.GetPropertiesAsync();
            Assert.Equal(layout.LengthOfStripe(stripeIndex), properties.Value.ContentLength);
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 20)]
    [InlineData(0, 100)]
    [InlineData(45, 55)]
    [InlineData(99, 99)]
    public async Task StripedDownload_RangesAcrossStripeBoundaries_ReturnCorrectContent(int from, int to)
    {
        // Arrange
        var content = CreateContent(100);
        var fileTransferId = await UploadStripedFile(content);
        var request = new HttpRequestMessage(HttpMethod.Get, $"broker/api/v1/filetransfer/{fileTransferId}/download");
        request.Headers.Range = new RangeHeaderValue(from, to);

        // Act
        var response = await _recipientClient.SendAsync(request);

        // Assert
        var clampedTo = Math.Min(to, content.Length - 1);
        var expected = content.AsSpan(from, clampedTo - from + 1).ToArray();
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal($"bytes {from}-{clampedTo}/{content.Length}", response.Content.Headers.ContentRange?.ToString());
    }

    [Fact]
    public async Task StripedUpload_ChunksTooSmallForTheBlockBudget_AreRejectedOnTheFirstPatch()
    {
        // Arrange
        var fileTransferId = await InitializeFileTransfer();
        var location = await CreatePartial(fileTransferId, 100);
        var request = new HttpRequestMessage(HttpMethod.Patch, location)
        {
            Content = new ByteArrayContent(CreateContent(1))
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        request.Headers.Add("Tus-Resumable", "1.0.0");
        request.Headers.Add("Upload-Offset", "0");

        // Act
        var response = await _senderClient.SendAsync(request);

        // Assert
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
    }
}

public class TusStripedPurgeTests(StripedStorageWebApplicationFactory factory)
    : StripedUploadTestBase(factory), IClassFixture<StripedStorageWebApplicationFactory>
{
    [Fact]
    public async Task DeleteFile_RemovesEveryStripe()
    {
        // Arrange
        var fileTransferId = await UploadStripedFile(CreateContent(100));
        var containerClient = GetContainerClient();
        var blobsBeforeDelete = await ListBlobs(containerClient, fileTransferId);
        Assert.True(blobsBeforeDelete.Count > 1, $"Expected several stripes, found {blobsBeforeDelete.Count}");

        using var scope = _factory.Services.CreateScope();
        var fileTransferRepository = scope.ServiceProvider.GetRequiredService<IFileTransferRepository>();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IResourceRepository>();
        var serviceOwnerRepository = scope.ServiceProvider.GetRequiredService<IServiceOwnerRepository>();
        var storageService = scope.ServiceProvider.GetRequiredService<IBrokerStorageService>();

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, CancellationToken.None);
        Assert.NotNull(fileTransfer);
        var resource = await resourceRepository.GetResource(fileTransfer!.ResourceId, CancellationToken.None);
        Assert.NotNull(resource);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource!.ServiceOwnerId);
        Assert.NotNull(serviceOwner);

        // Act
        await storageService.DeleteFile(serviceOwner!, fileTransfer, CancellationToken.None);
        var deleteAgain = () => storageService.DeleteFile(serviceOwner!, fileTransfer, CancellationToken.None);

        // Assert
        Assert.Empty(await ListBlobs(containerClient, fileTransferId));
        await deleteAgain();
    }
}
