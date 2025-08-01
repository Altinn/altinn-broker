﻿using System.Net.Http.Json;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Common.PEP.Helpers;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Authorization;
public class AltinnAuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IResourceRepository _resourceRepository;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IResourceRepository resourceRepository, ILogger<AltinnAuthorizationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _resourceRepository = resourceRepository;
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
        return await CheckAccessAsSender(user, fileTransfer.ResourceId, fileTransfer.Sender.ActorExternalId.WithoutPrefix(), isLegacyUser, cancellationToken) || await CheckAccessAsRecipient(user, fileTransfer, isLegacyUser, cancellationToken);
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
        bool isMaskinportenToken = user.Claims.Any(c => c.Type == "consumer" && c.Issuer.Contains("maskinporten.no"));
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequest(user, party.WithoutPrefix(), fileTransferId, rights, resource.Id, isMaskinportenToken);
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
        var validationResult = ValidateResult(responseContent, user, isMaskinportenToken);
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

    private XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, string party, string? fileTransferId, List<ResourceAccessLevel> actionTypes, string resourceId, bool isMaskinportenToken)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>()
        };
        // Use appropriate subject category creation based on token type
        var subjectCategory = isMaskinportenToken 
            ? XacmlMappers.CreateMaskinportenSubjectCategory(user)     // Handle Maskinporten consumer claims
            : DecisionHelper.CreateSubjectCategory(user.Claims);               // Use standard Altinn token handling
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

    private static bool ValidateResult(XacmlJsonResponse response, ClaimsPrincipal user, bool isMaskinportenToken)
    {
        
        foreach (var decision in response.Response)
        {
            bool result;
            
            if (isMaskinportenToken)
            {
                // For Maskinporten tokens: just check permit/deny, skip obligations
                result = decision.Decision.Equals(XacmlContextDecision.Permit.ToString());
            }
            else
            {
                // For Altinn tokens: use full validation with obligations
                result = DecisionHelper.ValidateDecisionResult(decision, user);
            }
            
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
