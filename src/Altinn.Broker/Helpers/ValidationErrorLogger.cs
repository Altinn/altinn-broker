using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Helpers;

public static class ValidationErrorLogger
{
    public static void LogValidationError(ActionContext context)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        foreach (var entry in context.ModelState)
        {
            foreach (var error in entry.Value.Errors)
            {
                logger.LogWarning("Validation error: " + error.ErrorMessage);
            }
        }
    }
}
