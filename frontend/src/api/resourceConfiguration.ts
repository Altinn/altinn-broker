import { ApiError, apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const RESOURCE_PATH = `${BROKER_API_PREFIX}/resource`

/** The ceiling the API enforces for virus scanned transfers (ApplicationConstants.MaxVirusScanUploadSize). */
const MAX_VIRUS_SCAN_FILE_SIZE = 50 * 1000 * 1000 * 1000

/** The parts of the API's ResourceExt that the form needs. */
export type ResourceConfiguration = {
  maxFileTransferSize: number | null
  /** Must be either sender or the single recipient of every transfer on the resource. */
  requiredParty: string | null
  /** Approval is granted by Altinn, so virus scanning is mandatory on all other resources. */
  approvedForDisabledVirusScan: boolean
}

/**
 * Reads the broker configuration for a resource. Open to any signed in caller, so no party has to
 * be supplied — the session cookie is what the API requires.
 */
export async function getResourceConfiguration(resourceId: string): Promise<ResourceConfiguration> {
  try {
    const body = await apiFetch<Partial<ResourceConfiguration>>(
      `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}`,
      { redirectOnUnauthorized: false },
    )
    return toConfiguration(body)
  } catch (error) {
    // Falling back to the mock keeps the form bounded instead of leaving it without limits.
    if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
      return toConfiguration(MOCK_CONFIGURATIONS[resourceId] ?? {})
    }
    throw error
  }
}

export function resolveMaxFileTransferSize(configuration: ResourceConfiguration): number {
  return configuration.maxFileTransferSize ?? MAX_VIRUS_SCAN_FILE_SIZE
}

function toConfiguration(body: Partial<ResourceConfiguration>): ResourceConfiguration {
  return {
    maxFileTransferSize: body.maxFileTransferSize ?? null,
    requiredParty: body.requiredParty ?? null,
    approvedForDisabledVirusScan: body.approvedForDisabledVirusScan ?? false,
  }
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
