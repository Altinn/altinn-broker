using System.Net.Http.Json;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Authorization;
public class AltinnAuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IResourceRepository _resourceRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IResourceRepository resourceRepository, IHostEnvironment hostEnvironment, ILogger<AltinnAuthorizationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _resourceRepository = resourceRepository;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string party, bool isLegacyUser, CancellationToken cancellationToken = default)
        => CheckUserAccess(user, resourceId, party, null, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, isLegacyUser, cancellationToken);

    public async Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, bool isLegacyUser, CancellationToken cancellationToken = default)
    {
        var recipients = fileTransfer.RecipientCurrentStatuses.DistinctBy(recipient => recipient.Actor.ActorExternalId);
        foreach (var recipient in recipients)
        {
            if (await CheckUserAccess(user, fileTransfer.ResourceId, recipient.Actor.ActorExternalId.WithoutPrefix(), fileTransfer.FileTransferId.ToString(), new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, isLegacyUser, cancellationToken))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<bool> CheckAccessForSearch(ClaimsPrincipal? user, string resourceId, string party, bool isLegacyUser, CancellationToken cancellationToken = default)
    {
        return await CheckUserAccess(user, resourceId, party, null, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, isLegacyUser, cancellationToken);
    }

    public async Task<bool> CheckAccessAsSenderOrRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, bool isLegacyUser, CancellationToken cancellationToken = default)
    {
        return await CheckAccessAsSender(user, fileTransfer.ResourceId, "", isLegacyUser, cancellationToken) || await CheckAccessAsRecipient(user, fileTransfer, isLegacyUser, cancellationToken);
    }

    private async Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, string party, string? fileTransferId, List<ResourceAccessLevel> rights, bool isLegacyUser, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        if (isLegacyUser)
        {
            return true;
        }
        var resource = await _resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is null)
        {
            _logger.LogWarning("Resource not found");
            return false;
        }
        var bypass = await EvaluateBypassConditions(isLegacyUser, resource, cancellationToken);
        if (bypass.HasValue)
        {
            return bypass.Value;
        }
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequest(user, party.WithoutPrefix(), fileTransferId, rights, resource.Id);
        var response = await _httpClient.PostAsJsonAsync("authorization/api/v1/authorize", jsonRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<XacmlJsonResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization.");
            return false;
        }
        var validationResult = ValidateResult(responseContent, user);
        return validationResult;
    }
    private async Task<bool?> EvaluateBypassConditions(bool isLegacyUser, ResourceEntity resource, CancellationToken cancellationToken)
    {
        if (isLegacyUser)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(resource.ServiceOwnerId))
        {
            _logger.LogWarning("Service owner not found for resource");
            return false;
        }
        return null;
    }

    private XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, string party, string? fileTransferId, List<ResourceAccessLevel> actionTypes, string resourceId)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>()
        };
        var subjectCategory = DecisionHelper.CreateSubjectCategory(user.Claims);
        request.AccessSubject.Add(subjectCategory);
        foreach (var actionType in actionTypes)
        {
            request.Action.Add(XacmlMappers.CreateActionCategory(GetActionId(actionType)));
        }
        var resourceCategory = XacmlMappers.CreateResourceCategory(resourceId, party, fileTransferId);
        request.Resource.Add(resourceCategory);
        XacmlJsonRequestRoot jsonRequest = new() { Request = request };
        return jsonRequest;
    }

    private static bool ValidateResult(XacmlJsonResponse response, ClaimsPrincipal user)
    {
        foreach (var decision in response.Response)
        {
            var result = DecisionHelper.ValidateDecisionResult(decision, user);
            if (result == false)
            {
                return false;
            }
        }
        return true;
    }

    private string GetActionId(ResourceAccessLevel right)
    {
        return right switch
        {
            ResourceAccessLevel.Read => "read",
            ResourceAccessLevel.Write => "write",
            _ => throw new NotImplementedException()
        };
    }
}
