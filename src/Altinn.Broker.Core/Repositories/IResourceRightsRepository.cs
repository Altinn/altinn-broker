﻿using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceRightsRepository
{
    Task<bool> CheckUserAccess(string resourceId, string userId, ResourceAccessLevel right, bool IsLegacyUser = false);
}
