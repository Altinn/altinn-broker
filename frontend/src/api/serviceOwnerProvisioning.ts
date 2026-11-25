import { ApiError, apiFetch, apiRequest, readBody } from './client'
import { BROKER_API_PREFIX } from './config'

/** Storage provider deployment status from GET /broker/api/v1/serviceowner. */
export type StorageProviderDeployment = {
  type: 'AltinnAzure' | 'AltinnAzureWithoutVirusScan'
  deploymentStatus: 'NotStarted' | 'DeployingResources' | 'Ready'
  deploymentEnvironment: string
}

export type ServiceOwnerOverview = {
  name: string
  storageProviders: StorageProviderDeployment[]
}

export type ServiceOwnerProvisioning = {
  status: 'NotRegistered' | 'InProgress' | 'Ready'
  percent: number
  messageKey: 'page.services.provision_phase_waiting' | 'page.services.provision_phase_deploying'
}

const VIRUS_SCAN_STORAGE = 'AltinnAzure'
const PLAIN_STORAGE = 'AltinnAzureWithoutVirusScan'

/**
 * Progress from the storage providers InitializeServiceOwner creates and GetServiceOwner reports.
 * A missing service owner is not registered. An existing row with no ready providers is still deploying.
 */
export function provisioningFromOverview(overview: ServiceOwnerOverview | null): ServiceOwnerProvisioning {
  if (overview === null) {
    return { status: 'NotRegistered', percent: 0, messageKey: 'page.services.provision_phase_waiting' }
  }

  const providers = overview.storageProviders ?? []
  const environment = providers[0]?.deploymentEnvironment ?? ''
  const required = environment.toLowerCase() === 'development'
    ? [VIRUS_SCAN_STORAGE]
    : [VIRUS_SCAN_STORAGE, PLAIN_STORAGE]
  const readyCount = required.filter((type) =>
    providers.some((provider) => provider.type === type && provider.deploymentStatus === 'Ready'),
  ).length

  if (providers.length > 0 && readyCount === required.length) {
    return { status: 'Ready', percent: 100, messageKey: 'page.services.provision_phase_deploying' }
  }

  const percent = providers.length === 0 ? 40 : Math.round(40 + (50 * readyCount) / required.length)
  return {
    status: 'InProgress',
    percent,
    messageKey: providers.length === 0
      ? 'page.services.provision_phase_waiting'
      : 'page.services.provision_phase_deploying',
  }
}

/** Null when the organization is not a service owner yet. */
export async function fetchServiceOwner(party: string): Promise<ServiceOwnerOverview | null> {
  const params = new URLSearchParams({ onBehalfOf: party })
  try {
    return await apiFetch<ServiceOwnerOverview>(`${BROKER_API_PREFIX}/serviceowner?${params}`)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null
    }
    throw error
  }
}

export async function initializeServiceOwner(party: string, name: string): Promise<void> {
  const params = new URLSearchParams({ onBehalfOf: party })
  const response = await apiRequest(`${BROKER_API_PREFIX}/serviceowner?${params}`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ name }),
  })
  if (!response.ok) {
    throw new ApiError(`Request failed: ${response.status}`, response.status, await readBody(response))
  }
}
