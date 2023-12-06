using Altinn.Broker.Application;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Mappers;

public static class ErrorMapper
{
    public static ActionResult ToActionResult(this Error error) => new ContentResult()
    {
        StatusCode = (int)error.StatusCode,
        Content = error.Message,
        ContentType = "text/plain; charset=utf-8"
    };
}
