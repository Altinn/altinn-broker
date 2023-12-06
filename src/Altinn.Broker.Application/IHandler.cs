using Altinn.Broker.Application;

using OneOf;

namespace Altinn.Broker.Core.Application;
internal interface IHandler<TRequest, TResponse>
{
    Task<OneOf<TResponse, Error>> Process(TRequest request);
}

// Return OneOf<TResponse,ApplicationError> instead in order to handle errors without exception0
