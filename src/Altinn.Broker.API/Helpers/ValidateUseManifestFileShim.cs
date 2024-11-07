using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Helpers
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ValidateUseManifestFileShim : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var useManifestFileShimProperty = validationContext.ObjectType.GetProperty("UseManifestFileShim");
            var externalServiceCodeLegacyProperty = validationContext.ObjectType.GetProperty("ExternalServiceCodeLegacy");
            var externalServiceEditionCodeLegacyProperty = validationContext.ObjectType.GetProperty("ExternalServiceEditionCodeLegacy");
            var useManifestFileShimValue = (bool?)useManifestFileShimProperty.GetValue(validationContext.ObjectInstance, null);
            var externalServiceCodeLegacyValue = externalServiceCodeLegacyProperty.GetValue(validationContext.ObjectInstance, null);
            var externalServiceEditionCodeLegacyValue = externalServiceEditionCodeLegacyProperty.GetValue(validationContext.ObjectInstance, null);

            if (useManifestFileShimValue == true)
            {
                if (externalServiceCodeLegacyValue == null || (externalServiceCodeLegacyValue is string strValue && string.IsNullOrEmpty(strValue)))
                {
                    return new ValidationResult("ExternalServiceCodeLegacy must be set and not be an empty string.");
                }

                if (externalServiceEditionCodeLegacyValue == null || (externalServiceEditionCodeLegacyValue is int intValue && intValue == 0))
                {
                    return new ValidationResult("ExternalServiceEditionCodeLegacy must be set and not be zero.");
                }
            }
            return ValidationResult.Success;
        }
    }
}