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

        if (options.MaxStripes <= 0)
        {
            failures.Add($"{nameof(AzureStorageOptions.MaxStripes)} must be greater than 0.");
        }

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

        if (options.MinStripeBytes <= 0)
        {
            failures.Add($"{nameof(AzureStorageOptions.MinStripeBytes)} must be greater than 0.");
        }

        if (options.MaxStripeBytes < options.MinStripeBytes)
        {
            failures.Add(
                $"{nameof(AzureStorageOptions.MaxStripeBytes)} must be greater than or equal to " +
                $"{nameof(AzureStorageOptions.MinStripeBytes)}.");
        }

        // A partial upload's block index is namespaced as D6, and a partial can contribute at most
        // MaxStripes x MaxBlocksPerStripe blocks. Exceeding six digits would produce block ids that
        // Azure rejects at commit time - at the very end of a multi-hour upload.
        if (options.MaxStripes > 0
            && options.MaxBlocksPerStripe > 0
            && (long)options.MaxStripes * options.MaxBlocksPerStripe > TusBlockIds.MaxNamespacedIndex)
        {
            failures.Add(
                $"{nameof(AzureStorageOptions.MaxStripes)} x {nameof(AzureStorageOptions.MaxBlocksPerStripe)} " +
                $"({(long)options.MaxStripes * options.MaxBlocksPerStripe}) must not exceed " +
                $"{TusBlockIds.MaxNamespacedIndex}, the largest block index a namespaced TUS block id can hold.");
        }

        // Malware scan results are matched back to a file transfer by the blob's name, which only works
        // for a single blob. Keeping the smallest stripe above the virus scan ceiling makes a scanned
        // transfer structurally incapable of being striped.
        if (!hostEnvironment.IsDevelopment() && options.MinStripeBytes < ApplicationConstants.MaxVirusScanUploadSize)
        {
            failures.Add(
                $"{nameof(AzureStorageOptions.MinStripeBytes)} must be at least " +
                $"{ApplicationConstants.MaxVirusScanUploadSize} bytes so that virus scanned transfers are never striped.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
