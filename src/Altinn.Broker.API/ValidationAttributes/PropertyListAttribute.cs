using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.API.ValidationAttributes
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class PropertyListAttribute : ValidationAttribute
    {
        public PropertyListAttribute()
        {
        }

        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success!;
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

                if (keyValuePair.Value.Length > 3000)
                    return new ValidationResult(String.Format("PropertyList Value can not be longer than 3000. Length:{0}, Value:{1}", keyValuePair.Value.Length.ToString(), keyValuePair.Value));
            }

            return ValidationResult.Success!;
        }
    }
}