﻿using System.Resources;
using System.Xml;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.ConfigureResourceCommand;
public class ConfigureResourceCommandHandler : IHandler<ConfigureResourceCommandRequest, Task>
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IAuthorizationService _resourceRightsRepository;

    public ConfigureResourceCommandHandler(IResourceRepository resourceRepository, IAuthorizationService resourceRightsRepository)
    {
        _resourceRepository = resourceRepository;
        _resourceRightsRepository = resourceRightsRepository;
    }

    public async Task<OneOf<Task, Error>> Process(ConfigureResourceCommandRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, request.Token.ClientId, [ResourceAccessLevel.Write], false, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var resource = await _resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.ResourceNotConfigured;
        }

        if (request.MaxFileTransferSize is not null)
        {
            var updateMaxFileTransferSizeResult = await UpdateMaxFileTransferSize(resource, request.MaxFileTransferSize.Value, cancellationToken);
            if (updateMaxFileTransferSizeResult.IsT1)
            {
                return updateMaxFileTransferSizeResult.AsT1;
            }
        }
        if (request.FileTransferTimeToLive is not null)
        {
            var updateFileTransferTimeToLiveResult = await UpdateFileTransferTimeToLive(resource, request.FileTransferTimeToLive, cancellationToken);
            if (updateFileTransferTimeToLiveResult.IsT1)
            {
                return updateFileTransferTimeToLiveResult.AsT1;
            }
        }
        return Task.CompletedTask;
    }

    private async Task<OneOf<Task, Error>> UpdateMaxFileTransferSize(ResourceEntity resource, long maxFileTransferSize, CancellationToken cancellationToken)
    {
        if (maxFileTransferSize < 0)
        {
            return Errors.MaxUploadSizeCannotBeNegative;
        }
        if (maxFileTransferSize == 0)
        {
            return Errors.MaxUploadSizeCannotBeZero;
        }
        if (maxFileTransferSize == resource.MaxFileTransferSize)
        {
            return Errors.MaxUploadSizeAlreadySet;
        }
        long globalMaxFileTransferSize = long.Parse(Environment.GetEnvironmentVariable("MAX_FILE_UPLOAD_SIZE"));
        if (maxFileTransferSize > globalMaxFileTransferSize)
        {
            return Errors.MaxUploadSizeOverGlobal;
        }
        await _resourceRepository.UpdateMaxFileTransferSize(resource.Id, maxFileTransferSize, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task<OneOf<Task, Error>> UpdateFileTransferTimeToLive(ResourceEntity resource, string fileTransferTimeToLiveString, CancellationToken cancellationToken)
    {
        TimeSpan fileTransferTimeToLive;
        try
        {
            fileTransferTimeToLive = XmlConvert.ToTimeSpan(fileTransferTimeToLiveString);
        }
        catch (FormatException)
        {
            return Errors.InvalidTimeToLiveFormat;
        }
        if (fileTransferTimeToLive > TimeSpan.FromDays(365))
        {
            return Errors.TimeToLiveCannotExceed365Days;
        }
        await _resourceRepository.UpdateFileRetention(resource.Id, fileTransferTimeToLiveString, cancellationToken);
        return Task.CompletedTask;
    }
}
