using Altinn.Broker.Persistence.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

// Custom exception class for testing transient behavior
public class TestTransientNpgsqlException : NpgsqlException
{
    public TestTransientNpgsqlException(string message) : base(message, new Exception()) { }
    public override bool IsTransient => true;
}

public class TestNonTransientNpgsqlException : NpgsqlException
{
    public TestNonTransientNpgsqlException(string message) : base(message, new Exception()) { }
    public override bool IsTransient => false;
}

public class DatabaseExecutorTests
{
    private readonly Mock<ILogger<ExecuteDBCommandWithRetries>> _mockLogger;
    private readonly ExecuteDBCommandWithRetries _executor;

    public DatabaseExecutorTests()
    {
        _mockLogger = new Mock<ILogger<ExecuteDBCommandWithRetries>>();
        _executor = new ExecuteDBCommandWithRetries(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteWithRetry_SucceedsOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "success";
        var operation = new Func<CancellationToken, Task<string>>(_ => Task.FromResult(expectedResult));

        // Act
        var result = await _executor.ExecuteWithRetry(operation);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify no warning logs were called (no retries happened)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteWithRetry_SucceedsAfterOneRetry_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "success after retry";
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                // First attempt fails with transient exception
                throw new TestTransientNpgsqlException("Transient error");
            }
            return Task.FromResult(expectedResult);
        });

        // Act
        var result = await _executor.ExecuteWithRetry(operation);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);

        // Verify warning log was called once for the retry
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database command attempt 1 failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteWithRetry_SucceedsAfterTwoRetries_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "success after two retries";
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            if (attemptCount <= 2)
            {
                // First two attempts fail with transient exception
                throw new TestTransientNpgsqlException("Transient error");
            }
            return Task.FromResult(expectedResult);
        });

        // Act
        var result = await _executor.ExecuteWithRetry(operation);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(3, attemptCount);

        // Verify warning logs were called twice for the retries
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database command attempt 1 failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database command attempt 2 failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteWithRetry_FailsAfterAllRetries_ThrowsOriginalException()
    {
        // Arrange
        const string errorMessage = "Persistent transient error";
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            // Always throw transient exception
            throw new TestTransientNpgsqlException(errorMessage);
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TestTransientNpgsqlException>(
            () => _executor.ExecuteWithRetry(operation));

        Assert.Equal(errorMessage, exception.Message);
        Assert.Equal(4, attemptCount); // Initial attempt + 3 retries

        // Verify all retry warning logs were called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database command attempt") && v.ToString().Contains("failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));

        // Verify error log was called once for final failure
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Exception during database command retries")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteWithRetry_NonTransientException_ThrowsImmediately()
    {
        // Arrange
        const string errorMessage = "Non-transient error";
        var attemptCount = 0;
        var operation = new Func<CancellationToken, Task<string>>(_ =>
        {
            attemptCount++;
            // Throw non-transient exception
            throw new TestNonTransientNpgsqlException(errorMessage);
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TestNonTransientNpgsqlException>(
            () => _executor.ExecuteWithRetry(operation));

        Assert.Equal(errorMessage, exception.Message);
        Assert.Equal(1, attemptCount); // Only one attempt, no retries

        // Verify no warning logs were called (no retries for non-transient exceptions)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteWithRetry_CancellationRequested_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var operation = new Func<CancellationToken, Task<string>>(async cancellationToken =>
        {
            // Simulate some work, then check cancellation
            await Task.Delay(10, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return "should not reach here";
        });

        // Cancel the token before execution
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _executor.ExecuteWithRetry(operation, cts.Token));
    }

    [Fact]
    public async Task ExecuteWithRetry_WithNullLogger_WorksCorrectly()
    {
        // Arrange
        var executorWithNullLogger = new ExecuteDBCommandWithRetries(new NullLogger<ExecuteDBCommandWithRetries>());
        const string expectedResult = "success";
        var operation = new Func<CancellationToken, Task<string>>(_ => Task.FromResult(expectedResult));

        // Act
        var result = await executorWithNullLogger.ExecuteWithRetry(operation);

        // Assert
        Assert.Equal(expectedResult, result);
    }
}
