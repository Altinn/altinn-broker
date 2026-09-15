import type { AuthorizedParty } from '@altinn/altinn-components'
import type { AuthorizedPartyDto } from '../api/parties'

/** Maps the Broker API response onto the shape the account selector expects. */
export function toAuthorizedParty(party: AuthorizedPartyDto): AuthorizedParty {
  return {
    partyUuid: party.partyUuid,
    name: party.name ?? '',
    organizationNumber: party.organizationNumber ?? undefined,
    partyId: String(party.partyId),
    type: party.type,
    isDeleted: party.isDeleted,
    onlyHierarchyElementWithNoAccess: party.onlyHierarchyElementWithNoAccess,
    // Broker does not ask Access Management for roles or resources per party, only for the party list.
    authorizedResources: [],
    authorizedRoles: [],
    subunits: party.subunits?.map(toAuthorizedParty),
  }
}

/** Every party in the hierarchy, parents before their subunits. */
export function flattenParties(parties: AuthorizedPartyDto[]): AuthorizedPartyDto[] {
  return parties.flatMap((party) => [party, ...flattenParties(party.subunits ?? [])])
}

/**
 * A party can only be acted on behalf of when it has an organization number: the Broker API
 * identifies parties by organization number, and a hierarchy element without access is not a
 * party the user actually represents.
 */
export function canBeRepresented(party: AuthorizedPartyDto): boolean {
  return Boolean(party.organizationNumber) && !party.onlyHierarchyElementWithNoAccess && !party.isDeleted
}

/** 922194912 → 922 194 912 */
export function formatOrganizationNumber(organizationNumber: string): string {
  return organizationNumber.replace(/(\d{3})(?=\d)/g, '$1 ')
}
