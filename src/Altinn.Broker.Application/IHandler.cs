﻿using System.Security.Claims;

using Altinn.Broker.Application;

using OneOf;

namespace Altinn.Broker.Core.Application;
internal interface IHandler<TRequest, TResponse>
{
    Task<OneOf<TResponse, Error>> Process(TRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken);
}

