using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.API.Models
{
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
}