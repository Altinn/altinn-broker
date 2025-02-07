using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;
using Xunit.Abstractions;

namespace Altinn.Broker.Tests.TestingFeature;

public class FileTransferControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public FileTransferControllerTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeFiletransfer_WithValidAcceptHeader_ReturnsOk()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeFileTransferResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeFiletransfer_WithNoAcceptHeader_ReturnsNotAcceptable()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, initializeFileTransferResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeFiletransfer_WithInvalidAcceptHeader_ReturnsNotAcceptable()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, initializeFileTransferResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeFiletransfer_WithValidAcceptHeaderAmongMultipleAcceptHeadersWithQualityFactors_ReturnsOk()
    {
        // Arrange
        _senderClient.DefaultRequestHeaders.Accept.Clear();
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain", 0.9));
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/markdown", 0.8));
        _senderClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.5));

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeFileTransferResponse.StatusCode);
    }
}