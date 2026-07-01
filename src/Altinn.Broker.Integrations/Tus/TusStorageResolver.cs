using System.Collections.Concurrent;

using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Azure;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public interface ITusStorageResolver
{
    Task<AzureBlobTusStore?> GetStoreForFileAsync(string fileId, CancellationToken cancellationToken);
}

public class TusStorageResolver(
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IHostEnvironment hostEnvironment,
    ITusExpirationDetailsStore expirationDetailsStore,
    IHttpContextAccessor httpContextAccessor) : ITusStorageResolver
{
    private readonly ConcurrentDictionary<string, AzureBlobTusStore> _stores = new(StringComparer.OrdinalIgnoreCase);

    public async Task<AzureBlobTusStore?> GetStoreForFileAsync(string fileId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(fileId, out var fileTransferId))
        {
            return null;
        }

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return null;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return null;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return null;
        }

        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        if (storageProvider is null)
        {
            return null;
        }

        var connectionString = hostEnvironment.IsDevelopment()
            ? AzureConstants.AzuriteUrl
            : $"https://{storageProvider.ResourceName}.blob.core.windows.net";
        var authenticationMode = hostEnvironment.IsDevelopment()
            ? AzureBlobTusStoreAuthenticationMode.ConnectionString
            : AzureBlobTusStoreAuthenticationMode.SystemAssignedManagedIdentity;

        return _stores.GetOrAdd(storageProvider.ResourceName, _ => new AzureBlobTusStore(
            connectionString,
            AzureStorageConstants.BrokerFilesContainerName,
            new AzureBlobTusStoreOptions
            {
                BlobPath = AzureStorageConstants.TusStagingBlobPath,
                AuthenticationMode = authenticationMode,
                ExpirationDetailsStore = expirationDetailsStore,
                FileIdGeneratorAsync = _ =>
                {
                    var httpContext = httpContextAccessor.HttpContext
                        ?? throw new InvalidOperationException("Missing HTTP context");
                    if (!TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
                    {
                        throw new InvalidOperationException("Missing file transfer id in route");
                    }

                    return Task.FromResult(fileTransferId.ToString());
                }
            }));
    }
}
