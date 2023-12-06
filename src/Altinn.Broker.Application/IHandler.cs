using Microsoft.AspNetCore.Mvc;

using OneOf;

namespace Altinn.Broker.Core.Application;
internal interface IHandler<TRequest, TResponse>
{
    Task<OneOf<TResponse, ActionResult>> Process(TRequest request);
}

// Return OneOf<TResponse,ApplicationError> instead in order to handle errors without exception0
