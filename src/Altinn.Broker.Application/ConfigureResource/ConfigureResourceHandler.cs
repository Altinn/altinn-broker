﻿using System.Xml;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Options;

using OneOf;

namespace Altinn.Broker.Application.ConfigureResource;
public class ConfigureResourceHandler : IHandler<ConfigureResourceRequest, Task>
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly long _maxFileUploadSize;
    private readonly string _maxGracePeriod;

    public ConfigureResourceHandler(IResourceRepository resourceRepository, IAuthorizationService resourceRightsRepository, IOptions<ApplicationSettings> applicationSettings)
    {
        _resourceRepository = resourceRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _maxFileUploadSize = applicationSettings.Value.MaxFileUploadSize;
        _maxGracePeriod = applicationSettings.Value.MaxGracePeriod;
    }

    public async Task<OneOf<Task, Error>> Process(ConfigureResourceRequest request, CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        }
        if (resource.ServiceOwnerId != request.Token.Consumer)
        {
            return Errors.NoAccessToResource;
        };

        if (request.PurgeFileTransferAfterAllRecipientsConfirmed is not null)
        {
            await _resourceRepository.UpdatePurgeFileTransferAfterAllRecipientsConfirmed(resource.Id, (bool)request.PurgeFileTransferAfterAllRecipientsConfirmed, cancellationToken);
        }
        if (request.PurgeFileTransferGracePeriod is not null)
        {
            var updatePurgeFileTransferGracePeriodResult = await UpdatePurgeFileTransferGracePeriod(resource, request.PurgeFileTransferGracePeriod, cancellationToken);
            if (updatePurgeFileTransferGracePeriodResult.IsT1)
            {
                return updatePurgeFileTransferGracePeriodResult.AsT1;
            }
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
        if (maxFileTransferSize > _maxFileUploadSize)
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
        await _resourceRepository.UpdateFileRetention(resource.Id, fileTransferTimeToLive, cancellationToken);
        return Task.CompletedTask;
    }
    private async Task<OneOf<Task, Error>> UpdatePurgeFileTransferGracePeriod(ResourceEntity resource, string PurgeFileTransferGracePeriodString, CancellationToken cancellationToken)
    {
        TimeSpan PurgeFileTransferGracePeriod;
        try
        {
            PurgeFileTransferGracePeriod = XmlConvert.ToTimeSpan(PurgeFileTransferGracePeriodString);
        }
        catch (FormatException)
        {
            return Errors.InvalidGracePeriodFormat;
        }
        if (PurgeFileTransferGracePeriod > XmlConvert.ToTimeSpan(_maxGracePeriod))
        {
            return Errors.GracePeriodCannotExceed24Hours;
        }
        await _resourceRepository.UpdatePurgeFileTransferGracePeriod(resource.Id, PurgeFileTransferGracePeriod, cancellationToken);
        return Task.CompletedTask;
    }
}
