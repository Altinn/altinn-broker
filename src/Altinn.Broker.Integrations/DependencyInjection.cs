﻿using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IResourceManager, AzureResourceManagerService>();
        services.AddSingleton<IBrokerStorageService, AzureBrokerStorageService>();
        services.AddSingleton<IFileStore, BlobService>();
    }
}