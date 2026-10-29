import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

/**
 * A formidlingstjeneste the logged-in end user has access to on behalf of a party,
 * as returned by GET /broker/api/v1/resource/authorized.
 */
export type AuthorizedResource = {
  resourceId: string
  name: string | null
  serviceOwnerName: string | null
  canSend: boolean
  canReceive: boolean
  /** Party is a Broker service owner and the user has publish on digdir-broker-administrasjon for that party. */
  canPublish: boolean
  /** The selected party is configured as a Broker service owner. */
  isServiceOwner: boolean
  /** The selected party owns this broker resource. */
  isOwned: boolean
}

export function fetchAuthorizedResources(party: string): Promise<AuthorizedResource[]> {
  const params = new URLSearchParams({ party })
  return apiFetch<AuthorizedResource[]>(`${BROKER_API_PREFIX}/resource/authorized?${params}`)
}
