using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Helpers;

namespace Altinn.Broker.Models;

/// <summary>
/// A model containing the metadata for a file transfer.
/// </summary>
public class FileTransferInitalizeExt
{
    /// <summary>
    /// The filename including extension
    /// </summary>
    [JsonPropertyName("fileName")]
    [StringLength(255, MinimumLength = 1)]
    [Required]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The Altinn resource ID
    /// </summary>
    [JsonPropertyName("resourceId")]
    [StringLength(255, MinimumLength = 1)]
    [Required]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Used by senders and receivers to identify specific file using external identification methods.
    /// </summary>
    [JsonPropertyName("sendersFileTransferReference")]
    [StringLength(4096, MinimumLength = 1)]
    public string SendersFileTransferReference { get; set; } = string.Empty;

    /// <summary>
    /// The sender organization of the file
    /// </summary>
    [JsonPropertyName("sender")]
    [RegularExpressionAttribute(Constants.OrgNumberPattern, ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
    [Required]
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// The recipient organizations of the broker fileTransfer
    /// </summary>
    [JsonPropertyName("recipients")]
    [ValidateElementsInList(typeof(RegularExpressionAttribute), Constants.OrgNumberPattern, ErrorMessage = "Each recipient should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
    [Required]
    [MinLength(1, ErrorMessage = "One or more recipients are required")]
    public List<string> Recipients { get; set; } = new List<string>();

    /// <summary>
    /// User-defined properties related to the file
    /// </summary>
    [JsonPropertyName("propertyList")]
    [PropertyList]
    public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// MD5 checksum for file data.
    /// </summary>
    [JsonPropertyName("checksum")]
    [MD5Checksum]
    public string? Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Disable virus scan. Requires special permission in production.
    /// </summary>
    [JsonPropertyName("disableVirusScan")]
    public bool? DisableVirusScan { get; set; } = false;
}

internal class MD5ChecksumAttribute : ValidationAttribute
{
    public MD5ChecksumAttribute()
    {
    }

    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        var stringValue = value as string;
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return ValidationResult.Success!;
        }
        if (stringValue.Length != 32)
        {
            return new ValidationResult("The checksum, if used, must be a MD5 hash with a length of 32 characters");
        }
        if (stringValue.ToLowerInvariant() != stringValue)
        {
            return new ValidationResult("The checksum, if used, must be a MD5 hash in lower case");
        }
        return ValidationResult.Success!;
    }
}

[AttributeUsage(AttributeTargets.Property)]
internal class PropertyListAttribute : ValidationAttribute
{
    public PropertyListAttribute()
    {
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (!(value is Dictionary<string, string>))
        {
            return new ValidationResult("PropertyList Object is not of proper type");
        }

        var dictionary = (Dictionary<string, string>)value;

        if (dictionary.Count > 10)
            return new ValidationResult("PropertyList can contain at most 10 properties");

        foreach (var keyValuePair in dictionary)
        {
            if (keyValuePair.Key.Length > 50)
                return new ValidationResult(String.Format("PropertyList Key can not be longer than 50. Length:{0}, KeyValue:{1}", keyValuePair.Key.Length.ToString(), keyValuePair.Key));

            if (keyValuePair.Value.Length > 300)
                return new ValidationResult(String.Format("PropertyList Value can not be longer than 300. Length:{0}, Value:{1}", keyValuePair.Value.Length.ToString(), keyValuePair.Value));
        }

        return ValidationResult.Success;
    }
}
