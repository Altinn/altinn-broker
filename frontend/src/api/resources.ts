import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

/**
 * A formidlingstjeneste the logged-in end user has access to on behalf of a party,
 * as returned by GET /broker/api/v1/resource/authorized.
 */
export type AuthorizedResource = {
  resourceId: string
  /** Title from the Resource Registry. Null when the resource is unavailable there. */
  name: string | null
  serviceOwnerName: string | null
  /** The party can initiate file transfers on the resource. */
  canSend: boolean
  /** The party can find and download file transfers on the resource. */
  canReceive: boolean
}

export function fetchAuthorizedResources(party: string): Promise<AuthorizedResource[]> {
  const params = new URLSearchParams({ party })
  return apiFetch<AuthorizedResource[]>(`${BROKER_API_PREFIX}/resource/authorized?${params}`)
}
