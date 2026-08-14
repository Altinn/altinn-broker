using Altinn.Broker.Core.Options;
using Altinn.Broker.Integrations.Azure;

using Microsoft.Extensions.Hosting;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class AzureStorageOptionsValidatorTests
{
    private static AzureStorageOptionsValidator CreateValidator(string? environmentName = null)
    {
        environmentName ??= Environments.Production;
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(environment => environment.EnvironmentName).Returns(environmentName);
        return new AzureStorageOptionsValidator(hostEnvironment.Object);
    }

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        // Arrange
        var validator = CreateValidator();
        var options = new AzureStorageOptions();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, string.Join("; ", result.Failures ?? []));
    }

    [Fact]
    public void Validate_MoreBlocksThanAzureAllows_Fails()
    {
        // Arrange
        var validator = CreateValidator();
        var options = new AzureStorageOptions { MaxBlocksPerStripe = 60_000 };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("blocks per blob"));
    }

    [Fact]
    public void Validate_StripeTooLargeToFillWithAzureSizedBlocks_Fails()
    {
        // Arrange
        var validator = CreateValidator();
        var options = new AzureStorageOptions { StripeSizeBytes = 6_000_000_000_000, MaxBlocksPerStripe = 50_000 };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("largest block Azure accepts"));
    }

    [Fact]
    public void Validate_StripeSmallerThanTheVirusScanCeiling_FailsOutsideDevelopment()
    {
        // Arrange
        var validator = CreateValidator();
        var options = new AzureStorageOptions { StripeSizeBytes = 1024 };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("virus scanned transfers"));
    }

    [Fact]
    public void Validate_StripeSmallerThanTheVirusScanCeiling_IsAllowedInDevelopment()
    {
        // Arrange
        var validator = CreateValidator(Environments.Development);
        var options = new AzureStorageOptions { StripeSizeBytes = 16, MaxBlocksPerStripe = 8 };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, string.Join("; ", result.Failures ?? []));
    }
}
