import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import type { ProvisioningPhase, ProvisioningStatus } from './resources'

/** Progress for registering a service owner and deploying its storage accounts. */
export type ServiceOwnerProvisioning = {
  status: ProvisioningStatus
  phase: ProvisioningPhase | null
  percent: number
}

export function fetchServiceOwnerProvisioning(party: string): Promise<ServiceOwnerProvisioning> {
  const params = new URLSearchParams({ party })
  return apiFetch<ServiceOwnerProvisioning>(`${BROKER_API_PREFIX}/serviceowner/provisioning?${params}`)
}

export function startServiceOwnerProvisioning(party: string, name: string): Promise<ServiceOwnerProvisioning> {
  const params = new URLSearchParams({ party })
  return apiFetch<ServiceOwnerProvisioning>(`${BROKER_API_PREFIX}/serviceowner/provisioning?${params}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name }),
  })
}
