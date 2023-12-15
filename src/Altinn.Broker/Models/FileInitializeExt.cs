using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.Helpers;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// API input model for file initialization.
    /// </summary>
    public class FileInitalizeExt
    {
        /// <summary>
        /// The filename including extension
        /// </summary>
        [JsonPropertyName("filename")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersFileReference")]
        [StringLength(4096, MinimumLength = 1)]
        public string SendersFileReference { get; set; } = string.Empty;

        /// <summary>
        /// The sender organization of the file
        /// </summary>
        [JsonPropertyName("sender")]
        [RegularExpressionAttribute(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// The recipient organizations of the broker file.
        /// </summary>
        [JsonPropertyName("recipients")]
        [ValidateElementsInList(typeof(RegularExpressionAttribute), @"^\d{4}:\d{9}$", ErrorMessage = "Each recipient should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// User-defined properties related to the file
        /// </summary>
        [JsonPropertyName("propertyList")]
        [MaxLength(10, ErrorMessage = "propertyList can contain at most 10 properties")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        [MD5Checksum]
        public string? Checksum { get; set; } = string.Empty;
    }
    internal class MD5ChecksumAttribute : ValidationAttribute
    {
        public MD5ChecksumAttribute()
        {
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return ValidationResult.Success;
            }
            if (stringValue.Length != 32)
            {
                return new ValidationResult("The checksum, if used, must be a MD5 hash with a length of 32 characters");
            }
            return ValidationResult.Success;
        }
    }
}
