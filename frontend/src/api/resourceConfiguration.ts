import { apiFetch } from './client'
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
  const body = await apiFetch<Partial<ResourceConfiguration>>(
    `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}`,
    { redirectOnUnauthorized: false },
  )

  return toConfiguration(body)
}

export function resolveMaxFileTransferSize(configuration: ResourceConfiguration): number {
  return configuration.maxFileTransferSize ?? MAX_VIRUS_SCAN_FILE_SIZE
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
