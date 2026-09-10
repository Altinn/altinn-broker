import { ApiError, apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const RESOURCE_PATH = `${BROKER_API_PREFIX}/resource`

/** The ceiling the API enforces for virus scanned transfers. */
const MAX_VIRUS_SCAN_FILE_SIZE = 50 * 1000 * 1000 * 1000

/** The parts of the API's ResourceExt that the form needs. */
export type ResourceConfiguration = {
  maxFileTransferSize: number | null
  requiredParty: string | null
  approvedForDisabledVirusScan: boolean
}

/**
 * Reads the broker configuration for a resource.
 */
export async function getResourceConfiguration(resourceId: string): Promise<ResourceConfiguration> {
  try {
    const body = await apiFetch<Partial<ResourceConfiguration>>(
      `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}`,
      { redirectOnUnauthorized: false },
    )
    return toConfiguration(body)
  } catch (error) {
    // Fall back to mock until the API endpoint is open to senders.
    if (isAccessDenied(error)) {
      return toConfiguration(MOCK_CONFIGURATIONS[resourceId])
    }
    throw error
  }
}

export function resolveMaxFileTransferSize(configuration: ResourceConfiguration): number {
  return configuration.maxFileTransferSize ?? MAX_VIRUS_SCAN_FILE_SIZE
}

function isAccessDenied(error: unknown): boolean {
  return error instanceof ApiError && [401, 403].includes(error.status)
}

const UNCONFIGURED: ResourceConfiguration = {
  maxFileTransferSize: null,
  requiredParty: null,
  approvedForDisabledVirusScan: false,
}

/** Absent fields fall back to the unconfigured defaults; nulls from the API are kept as null. */
function toConfiguration(body: Partial<ResourceConfiguration> = {}): ResourceConfiguration {
  return { ...UNCONFIGURED, ...body }
}

const MOCK_CONFIGURATIONS: Record<string, Partial<ResourceConfiguration>> = {
  'ttd-brokerbox-utvikling': {
    maxFileTransferSize: 2 * 1024 * 1024 * 1024,
    approvedForDisabledVirusScan: true,
  },
  avvik: {
    maxFileTransferSize: 100 * 1000 * 1000,
    requiredParty: '0192:974761211',
  },
  forskning: {
    maxFileTransferSize: 5 * 1000 * 1000 * 1000,
  }
}
