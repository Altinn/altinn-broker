﻿using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Common.PEP.Configuration;
using Altinn.Common.PEP.Helpers;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Authorization;
public class AltinnAuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IResourceRepository _resourceRepository;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IOptions<PlatformSettings> platformSettings, IHttpContextAccessor httpContextAccessor, IResourceRepository resourceRepository, ILogger<AltinnAuthorizationService> logger)
    {
        httpClient.BaseAddress = new Uri(altinnOptions.Value.PlatformGatewayUrl);
        httpClient.DefaultRequestHeaders.Add("Authorization", httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString());
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _resourceRepository = resourceRepository;
        _logger = logger;
    }

    public async Task<bool> CheckUserAccess(string resourceId, string userId, ResourceAccessLevel right, bool IsLegacyUser = false)
    {
        if (IsLegacyUser)
        {
            return true;
        }
        var resource = await _resourceRepository.GetResource(resourceId);
        if (resource is null)
        {
            return false;
        }
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            _logger.LogError("Unexpected null value. User was null when checking access to resource");
            return false;
        }
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequest(user, GetActionId(right), resource);
        var response = await _httpClient.PostAsJsonAsync("authorization/api/v1/authorize", jsonRequest);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<XacmlJsonResponse>();
        if (user is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization.");
            return false;
        }
        var validationResult = ValidateResult(responseContent);
        return validationResult;
    }

    private XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, string actionType, ResourceEntity resourceEntity)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>()
        };

        var subjectCategory = DecisionHelper.CreateSubjectCategory(user.Claims);
        subjectCategory.Attribute = subjectCategory.Attribute.Where(attribute => attribute.AttributeId != "urn:altinn:authlevel").ToList(); // Temp fix as xcaml int32 not implemented
        request.AccessSubject.Add(subjectCategory);
        request.Action.Add(XacmlMappers.CreateActionCategory(actionType));
        request.Action.Add(XacmlMappers.CreateActionCategory(actionType));
        request.Action.Add(XacmlMappers.CreateActionCategory(actionType));
        request.Resource.Add(XacmlMappers.CreateResourceCategory(resourceEntity));

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }
    private static bool ValidateResult(XacmlJsonResponse response)
    {
        if (response.Response[0].Decision.Equals(XacmlContextDecision.Permit.ToString()))
        {
            return true;
        }

        return false;
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