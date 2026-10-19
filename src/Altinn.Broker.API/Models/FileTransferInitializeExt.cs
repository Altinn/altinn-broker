using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.ValidationAttributes;
using Altinn.Broker.Enums;
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
    [ResourceIdentifier]
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
    [RegularExpressionAttribute(Constants.OrgNumberPattern, ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber (e.g. 0192:910753614) or urn:altinn:organization:identifier-no:organizationnumber")]
    [Required]
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// The recipient organizations of the broker fileTransfer
    /// </summary>
    [JsonPropertyName("recipients")]
    [ValidateElementsInList(typeof(RegularExpressionAttribute), Constants.OrgNumberPattern, ErrorMessage = "Each recipient should be on the form countrycode:organizationnumber (e.g. 0192:910753614) or urn:altinn:organization:identifier-no:organizationnumber")]
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
    /// Checksum for file data. MD5 is 32 lowercase hex characters. SHA256 is 64 lowercase hex characters.
    /// Leave empty to have Broker calculate it after upload.
    /// </summary>
    [JsonPropertyName("checksum")]
    [Checksum]
    public string? Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Algorithm for <see cref="Checksum"/>. Defaults to MD5.
    /// </summary>
    [JsonPropertyName("checksumAlgorithm")]
    public ChecksumAlgorithmExt ChecksumAlgorithm { get; set; } = ChecksumAlgorithmExt.MD5;

    /// <summary>
    /// Disable virus scan. Requires special permission in production.
    /// </summary>
    [JsonPropertyName("disableVirusScan")]
    public bool? DisableVirusScan { get; set; } = false;

    /// <summary>
    /// Optional notification to send to the recipient(s) once the file transfer is published.
    /// </summary>
    [JsonPropertyName("notification")]
    public NotificationRequestExt? Notification { get; set; }
}