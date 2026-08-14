using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;

/// <summary>
/// This class validates Azure storage options. Fails startup on a stripe configuration that would corrupt uploads rather than merely underperform.
/// </summary>
public sealed class AzureStorageOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<AzureStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureStorageOptions options)
    {
        var failures = new List<string>();

        if (options.MaxBlocksPerStripe <= 0)
        {
            failures.Add($"{nameof(AzureStorageOptions.MaxBlocksPerStripe)} must be greater than 0.");
        }

        if (options.MaxBlocksPerStripe > TusBlockIds.MaxBlocksPerBlob)
        {
            failures.Add(
                $"{nameof(AzureStorageOptions.MaxBlocksPerStripe)} must not exceed Azure's limit of " +
                $"{TusBlockIds.MaxBlocksPerBlob} blocks per blob.");
        }

        if (options.StripeSizeBytes <= 0)
        {
            failures.Add($"{nameof(AzureStorageOptions.StripeSizeBytes)} must be greater than 0.");
        }

        if (options.MaxBlocksPerStripe > 0
            && options.StripeSizeBytes > (long)options.MaxBlocksPerStripe * AzureStorageConstants.MaxBlockSizeBytes)
        {
            failures.Add(
                $"{nameof(AzureStorageOptions.StripeSizeBytes)} must not exceed " +
                $"{nameof(AzureStorageOptions.MaxBlocksPerStripe)} x {AzureStorageConstants.MaxBlockSizeBytes} bytes, " +
                $"the largest block Azure accepts.");
        }

        // Malware scan results are matched back to a file transfer by the blob's name, which only works
        // for a single blob. Keeping the stripe size above the virus scan ceiling makes a scanned
        // transfer structurally incapable of being striped.
        if (!hostEnvironment.IsDevelopment() && options.StripeSizeBytes < ApplicationConstants.MaxVirusScanUploadSize)
        {
            failures.Add(
                $"{nameof(AzureStorageOptions.StripeSizeBytes)} must be at least " +
                $"{ApplicationConstants.MaxVirusScanUploadSize} bytes so that virus scanned transfers are never striped.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
