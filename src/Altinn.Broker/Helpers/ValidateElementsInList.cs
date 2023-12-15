using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Helpers;

public class ValidateElementsInList : ValidationAttribute
{
    private readonly Type _attributeType;
    private readonly object[] _attributeArgs;

    public ValidateElementsInList(Type attributeType, params object[] attributeArgs)
    {
        _attributeType = attributeType;
        _attributeArgs = attributeArgs;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var list = value as IEnumerable<string>;
        if (list != null)
        {
            var attributeInstance = (ValidationAttribute)Activator.CreateInstance(_attributeType, _attributeArgs);
            foreach (var item in list)
            {
                if (!attributeInstance.IsValid(item))
                {
                    return new ValidationResult(attributeInstance.ErrorMessage);
                }
            }
        }

        return ValidationResult.Success;
    }
}
