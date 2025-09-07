using System.Net.Http.Json;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Common;
using Altinn.Broker.Common.Constants;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Common.PEP.Constants;
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
    private const string PolicyObligationMinAuthnLevel = "urn:altinn:minimum-authenticationlevel";
    private const string PolicyObligationMinAuthnLevelOrg = "urn:altinn:minimum-authenticationlevel-org";

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
        var subjectCategory = CreateSubjectCategory(user);
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

    private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
    {
        var subjectCategory = DecisionHelper.CreateSubjectCategory(user.Claims);
        var isSystemUserSubject = subjectCategory.Attribute.Any(attribute => attribute.AttributeId == AltinnXacmlUrns.SystemUserUuid);
        if (!isSystemUserSubject)
        {
            var pidClaim = user.Claims.FirstOrDefault(claim => claim.Type == "pid");
            if (pidClaim is not null)
            {
                subjectCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.PersonIdAttribute, pidClaim.Value, "string", pidClaim.Issuer));
            }
        }
        return subjectCategory;
    }

    private static bool ValidateResult(XacmlJsonResponse response, ClaimsPrincipal user, bool isMaskinportenToken)
    {
        foreach (var decision in response.Response)
        {
            bool result;

            // For Altinn tokens: use full validation with obligations
            result = ValidateDecisionResult(decision, user, isMaskinportenToken);
            
            if (result == false)
            {
                return false;
            }
        }
        return true;
    }

    private static bool ValidateDecisionResult(XacmlJsonResult result, ClaimsPrincipal user, bool isMaskinportenToken)
    {

        // Checks that the result is nothing else than "permit"
        if (!result.Decision.Equals(XacmlContextDecision.Permit.ToString()))
        {
            return false;
        }

        // Checks if the result contains obligation
        if (result.Obligations != null)
        {
            List<XacmlJsonObligationOrAdvice> obligationList = result.Obligations;
            XacmlJsonAttributeAssignment attributeMinLvAuth = GetObligation(PolicyObligationMinAuthnLevel, obligationList);

            // Checks if the obligation contains a minimum authentication level attribute
            if (attributeMinLvAuth != null)
            {
                string minAuthenticationLevel = attributeMinLvAuth.Value;
                string usersAuthenticationLevel = user.Claims.FirstOrDefault(c => c.Type.Equals("urn:altinn:authlevel"))?.Value;
                if (usersAuthenticationLevel is null && isMaskinportenToken)
                {
                    usersAuthenticationLevel = "3";
                }

                // Checks that the user meets the minimum authentication level
                if (Convert.ToInt32(usersAuthenticationLevel) < Convert.ToInt32(minAuthenticationLevel))
                {
                    if (user.Claims.FirstOrDefault(c => c.Type.Equals("urn:altinn:org")) != null)
                    {
                        XacmlJsonAttributeAssignment attributeMinLvAuthOrg = GetObligation(PolicyObligationMinAuthnLevelOrg, obligationList);
                        if (attributeMinLvAuthOrg != null)
                        {
                            if (Convert.ToInt32(usersAuthenticationLevel) >= Convert.ToInt32(attributeMinLvAuthOrg.Value))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }
        }

        return true;
    }

    private static XacmlJsonAttributeAssignment GetObligation(string category, List<XacmlJsonObligationOrAdvice> obligations)
    {
        foreach (XacmlJsonObligationOrAdvice obligation in obligations)
        {
            XacmlJsonAttributeAssignment assignment = obligation.AttributeAssignment.FirstOrDefault(a => a.Category.Equals(category));
            if (assignment != null)
            {
                return assignment;
            }
        }

        return null;
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
