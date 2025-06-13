using System.Net;
using Altinn.Broker.Core.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Broker.Tests;

public class HttpRetryPolicyTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    public HttpRetryPolicyTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://example.com")
        };
    }

    [Fact]
    public async Task StandardRetryPolicy_ShouldRetryOnTransientFailures()
    {
        // Arrange
        var policy = HttpClientBuilderExtensions.GetStandardRetryPolicy(_mockLogger.Object);
        var callCount = 0;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/test");
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(3, callCount);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task StandardRetryPolicy_ShouldFailAfterMaxRetries()
    {
        // Arrange
        var policy = HttpClientBuilderExtensions.GetStandardRetryPolicy(_mockLogger.Object);
        var callCount = 0;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            });

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/test");
        });

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal(4, callCount);
    }

    [Fact]
    public async Task StandardRetryPolicy_ShouldNotRetryOnNonTransientFailures()
    {
        // Arrange
        var policy = HttpClientBuilderExtensions.GetStandardRetryPolicy(_mockLogger.Object);
        var callCount = 0;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); // Non-transient failure
            });

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/test");
        });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(1, callCount);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task StandardRetryPolicy_ShouldRetryOnSpecificTransientFailures(HttpStatusCode statusCode)
    {
        // Arrange
        var policy = HttpClientBuilderExtensions.GetStandardRetryPolicy(_mockLogger.Object);
        var callCount = 0;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    return Task.FromResult(new HttpResponseMessage(statusCode));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/test");
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task StandardRetryPolicy_ShouldRetryOnHttpRequestException()
    {
        // Arrange
        var policy = HttpClientBuilderExtensions.GetStandardRetryPolicy(_mockLogger.Object);
        var callCount = 0;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new HttpRequestException("Network error");
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/test");
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task StandardRetryPolicy_ShouldRetryOnTaskCanceledException()
    {
        // Arrange
        var policy = HttpClientBuilderExtensions.GetStandardRetryPolicy(_mockLogger.Object);
        var callCount = 0;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new TaskCanceledException("Request timeout");
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/test");
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, callCount);
    }
} 