using System.ComponentModel.DataAnnotations;

using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.API.ValidationAttributes;

internal class ChecksumAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var stringValue = value as string;
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return ValidationResult.Success;
        }

        var algorithm = validationContext.ObjectInstance is FileTransferInitalizeExt initialize
            ? initialize.ChecksumAlgorithm
            : ChecksumAlgorithmExt.MD5;

        return algorithm switch
        {
            ChecksumAlgorithmExt.SHA256 => Validate(stringValue, expectedLength: 64, algorithmName: "SHA256"),
            ChecksumAlgorithmExt.MD5 => Validate(stringValue, expectedLength: 32, algorithmName: "MD5"),
            _ => new ValidationResult("The checksum algorithm must be MD5 or SHA256")
        };
    }

    private static ValidationResult Validate(string stringValue, int expectedLength, string algorithmName)
    {
        if (stringValue.Length != expectedLength)
        {
            return new ValidationResult($"The checksum, if used, must be a {algorithmName} hash with a length of {expectedLength} characters");
        }

        if (stringValue.ToLowerInvariant() != stringValue)
        {
            return new ValidationResult($"The checksum, if used, must be a {algorithmName} hash in lower case");
        }

        return ValidationResult.Success!;
    }
}
