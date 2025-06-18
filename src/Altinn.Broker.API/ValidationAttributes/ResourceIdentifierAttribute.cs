using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Altinn.Broker.Common.Constants;

namespace Altinn.Broker.API.ValidationAttributes;

public class ResourceIdentifierAttribute : ValidationAttribute
{
    private static readonly string Pattern = $@"^(?:{UrnConstants.Resource}:)?[^:]{{1,255}}$";
    private static readonly Regex Regex = new(Pattern);
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string stringValue || !IsValidResourceFormat(stringValue))
        {
            return new ValidationResult(ErrorMessage ?? "Invalid Resource identifier format");
        }

        return ValidationResult.Success;
    }

    public static bool IsValidResourceFormat(string value)
    {
        return string.IsNullOrEmpty(value) || Regex.IsMatch(value);
    }
}