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
  /** The user has publish on digdir-broker-administrasjon for the selected party. */
  hasConfigureAccess: boolean
  /** NotRegistered, InProgress, Ready, or Failed for the selected party. */
  provisioningStatus: ProvisioningStatus
  /** Current deployment step. Null when the party is not registered. */
  provisioningPhase: ProvisioningPhase | null
  /** Approximate provisioning completion, from 0 to 100. */
  provisioningPercent: number
}

export type ProvisioningStatus = 'NotRegistered' | 'InProgress' | 'Ready' | 'Failed'

export type ProvisioningPhase =
  | 'Queued'
  | 'CreatingResourceGroup'
  | 'CreatingVirusScanStorage'
  | 'EnablingMalwareProtection'
  | 'CreatingStorage'
  | 'Ready'
  | 'Failed'

export function fetchAuthorizedResources(party: string): Promise<AuthorizedResource[]> {
  const params = new URLSearchParams({ party })
  return apiFetch<AuthorizedResource[]>(`${BROKER_API_PREFIX}/resource/authorized?${params}`)
}
