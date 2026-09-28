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
  /** Party is a Broker service owner and the user has publish on ttd-brokerbox-utvikling for that party. */
  canPublish: boolean
}

export function fetchAuthorizedResources(party: string): Promise<AuthorizedResource[]> {
  const params = new URLSearchParams({ party })
  return apiFetch<AuthorizedResource[]>(`${BROKER_API_PREFIX}/resource/authorized?${params}`)
}
