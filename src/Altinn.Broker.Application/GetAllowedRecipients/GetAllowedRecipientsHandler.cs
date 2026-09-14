using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Platform.Register.Models;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetAllowedRecipients;

/// <summary>
/// Lists the organizations a party may address a file transfer to on a resource: the parties on the
/// resource's access lists, narrowed to what <see cref="Errors.RequiredPartyInvalidRecipientConfiguration"/>
/// and <see cref="Errors.RecipientNotInAccessList"/> would otherwise reject at initialization.
/// </summary>
public class GetAllowedRecipientsHandler(
    IResourceRepository resourceRepository,
    IAltinnResourceRepository altinnResourceRepository,
    IAltinnRegisterService registerService,
    ILogger<GetAllowedRecipientsHandler> logger) : IHandler<GetAllowedRecipientsRequest, List<AllowedRecipientOverview>>
{
    public async Task<OneOf<List<AllowedRecipientOverview>, Error>> Process(GetAllowedRecipientsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var sender = request.Party.WithoutPrefix();
        if (!sender.IsOrganizationNumber())
        {
            return Errors.InvalidParty;
        }

        var resource = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.ResourceHasNotBeenConfigured;
        }

        var accessList = await altinnResourceRepository.GetAccessListMembersOfResource(request.ResourceId, cancellationToken);
        if (accessList is null)
        {
            return Errors.InvalidResourceDefinition;
        }

        // A required party that is the sender itself puts no constraint on the recipients, which
        // mirrors how InitializeFileTransferHandler accepts the transfer.
        var requiredParty = resource.RequiredParty?.WithoutPrefix();
        if (!string.IsNullOrWhiteSpace(requiredParty) && requiredParty != sender)
        {
            return await RequiredPartyOnly(request.ResourceId, requiredParty, accessList.Restricted, cancellationToken);
        }

        var organizations = await ResolveOrganizations(accessList.PartyUuids, cancellationToken);
        var recipients = organizations
            .Where(organization => organization.OrganizationNumber != sender)
            .OrderBy(recipient => recipient.Name ?? recipient.OrganizationNumber, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        logger.LogInformation(
            "Resolved {recipientCount} allowed recipients from {partyCount} access list parties on resource {resourceId}",
            recipients.Count,
            accessList.PartyUuids.Count,
            request.ResourceId.SanitizeForLogs());
        return recipients;
    }

    /// <remarks>
    /// The required party is the only recipient the API will accept, so it is the whole list. On a
    /// resource with an access list it still has to be a member; without one nothing constrains it.
    /// </remarks>
    private async Task<List<AllowedRecipientOverview>> RequiredPartyOnly(string resourceId, string requiredParty, bool restricted, CancellationToken cancellationToken)
    {
        if (restricted)
        {
            var membership = await altinnResourceRepository.GetAccessListOfResource(resourceId, requiredParty, cancellationToken);
            if (membership is null || membership.Count == 0)
            {
                logger.LogWarning("Resource {resourceId} requires a party that is not on its access list, so it has no valid recipients", resourceId.SanitizeForLogs());
                return [];
            }
        }

        var organization = await ResolveOrganizationByNumber(requiredParty, cancellationToken);
        return [organization ?? new AllowedRecipientOverview { OrganizationNumber = requiredParty }];
    }

    private async Task<List<AllowedRecipientOverview>> ResolveOrganizations(List<string> partyUuids, CancellationToken cancellationToken)
    {
        var resolved = await Task.WhenAll(partyUuids.Select(partyUuid => ResolveOrganization(partyUuid, cancellationToken)));
        return resolved.OfType<AllowedRecipientOverview>().ToList();
    }

    /// <remarks>One unresolvable party should not cost the sender the rest of the list.</remarks>
    private async Task<AllowedRecipientOverview?> ResolveOrganization(string partyUuid, CancellationToken cancellationToken)
    {
        try
        {
            return ToRecipient(await registerService.LookupPartyByUuid(partyUuid, cancellationToken));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not look up access list party {partyUuid} in Altinn Register", partyUuid.SanitizeForLogs());
            return null;
        }
    }

    /// <remarks>A missing name must not drop the required party, since it is the only valid recipient.</remarks>
    private async Task<AllowedRecipientOverview?> ResolveOrganizationByNumber(string organizationNumber, CancellationToken cancellationToken)
    {
        try
        {
            return ToRecipient(await registerService.LookupPartyByOrganizationNumber(organizationNumber, cancellationToken));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not look up organization {organizationNumber} in Altinn Register", organizationNumber.SanitizeForLogs());
            return null;
        }
    }

    /// <remarks>A party without an organization number is a person, and cannot receive a file transfer.</remarks>
    private static AllowedRecipientOverview? ToRecipient(Party? party)
        => string.IsNullOrWhiteSpace(party?.OrgNumber)
            ? null
            : new AllowedRecipientOverview { OrganizationNumber = party.OrgNumber, Name = party.Name };
}
