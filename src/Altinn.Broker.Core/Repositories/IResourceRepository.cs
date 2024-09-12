﻿using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceRepository
{
    Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken = default);
    Task UpdateMaxFileTransferSize(string resourceId, long maxSize, CancellationToken cancellationToken = default);
    Task CreateResource(ResourceEntity resource, CancellationToken cancellationToken = default);
    Task UpdateFileRetention(string resourceId, TimeSpan fileTransferTimeToLive, CancellationToken cancellationToken = default);
    Task UpdatePurgeFileTransferAfterAllRecipientsConfirmed(string resourceId, bool PurgeFileTransferAfterAllRecipientsConfirmed, CancellationToken cancellationToken = default);
    Task UpdatePurgeFileTransferGracePeriod(string resourceId, TimeSpan PurgeFileTransferGracePeriod, CancellationToken cancellationToken = default);
}
