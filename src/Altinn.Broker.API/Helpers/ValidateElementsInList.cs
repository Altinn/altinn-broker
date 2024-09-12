using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Helpers;

public class ValidateElementsInList(Type attributeType, params object[] attributeArgs) : ValidationAttribute
{
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        var list = value as IEnumerable<string>;
        if (list != null)
        {
            var attributeInstance = (ValidationAttribute)Activator.CreateInstance(attributeType, attributeArgs)!;
            foreach (var item in list)
            {
                if (!attributeInstance.IsValid(item))
                {
                    return new ValidationResult(attributeInstance.ErrorMessage);
                }
            }
        }

        return ValidationResult.Success!;
    }
}
