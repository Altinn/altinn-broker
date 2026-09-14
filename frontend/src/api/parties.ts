import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

/**
 * A party the logged-in end user can act on behalf of,
 * as returned by GET /broker/api/v1/party/authorized.
 */
export type AuthorizedPartyDto = {
  partyUuid: string
  name: string | null
  /** Null for parties that are not organizations. */
  organizationNumber: string | null
  partyId: number
  /** 'Person', 'Organization', 'SelfIdentified' or 'None'. */
  type: string
  unitType: string | null
  isDeleted: boolean
  /** The party only carries its subunits; the user has no access to the party itself. */
  onlyHierarchyElementWithNoAccess: boolean
  subunits: AuthorizedPartyDto[]
}

export function fetchAuthorizedParties(): Promise<AuthorizedPartyDto[]> {
  return apiFetch<AuthorizedPartyDto[]>(`${BROKER_API_PREFIX}/party/authorized`)
}
