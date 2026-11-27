import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const RESOURCE_PATH = `${BROKER_API_PREFIX}/resource`

/** Max upload size when virus scanning is enabled (50 decimal GB). */
export const VIRUS_SCAN_MAX_SIZE_BYTES = 50 * 1000 * 1000 * 1000

/**
 * The broker configuration of a resource, with every field resolved to what the API applies.
 * Legacy Altinn 2 settings are left out.
 */
export type ResourceConfiguration = {
  maxFileTransferSize: number | null
  fileTransferTimeToLive: string
  purgeFileTransferAfterAllRecipientsConfirmed: boolean
  purgeFileTransferGracePeriod: string
  requiredParty: string | null
  approvedForDisabledVirusScan: boolean
}

type ResourceConfigurationBody = {
  [K in keyof ResourceConfiguration]?: ResourceConfiguration[K] | null
}

const DEFAULTS: ResourceConfiguration = {
  maxFileTransferSize: null,
  fileTransferTimeToLive: '30.00:00:00',
  purgeFileTransferAfterAllRecipientsConfirmed: true,
  purgeFileTransferGracePeriod: '02:00:00',
  requiredParty: null,
  approvedForDisabledVirusScan: false,
}

/**
 * Effective max for a transfer when virus scan is on: configured value capped at 50 GB,
 * or 50 GB when no limit is configured.
 */
export function effectiveMaxFileTransferSize(
  configured: number | null,
  useVirusScan: boolean,
): number | null {
  if (!useVirusScan) {
    return configured
  }

  return configured === null
    ? VIRUS_SCAN_MAX_SIZE_BYTES
    : Math.min(configured, VIRUS_SCAN_MAX_SIZE_BYTES)
}

/**
 * Fields the ConfigureResource endpoint accepts for a resource (legacy Altinn 2 settings omitted).
 * Durations must be ISO-8601 (e.g. `P30D`, `PT2H`).
 */
export type ConfigureResourceInput = {
  maxFileTransferSize?: number | null
  fileTransferTimeToLive?: string
  purgeFileTransferAfterAllRecipientsConfirmed?: boolean
  purgeFileTransferGracePeriod?: string
  requiredParty?: string | null
  approvedForDisabledVirusScan?: boolean
}

/** TT02, reached through the production API when staging configuration is enabled. */
export const TT02_ENVIRONMENT = 'tt02'

/**
 * Reads the broker configuration for a resource.
 * Pass `tt02` to read the staging resource instead of the current environment.
 */
export async function getResourceConfiguration(
  resourceId: string,
  environment?: string,
): Promise<ResourceConfiguration> {
  const body = await apiFetch<ResourceConfigurationBody>(
    resourcePath(resourceId, environment),
    { redirectOnUnauthorized: false },
  )

  return {
    maxFileTransferSize: body.maxFileTransferSize ?? DEFAULTS.maxFileTransferSize,
    fileTransferTimeToLive: body.fileTransferTimeToLive ?? DEFAULTS.fileTransferTimeToLive,
    purgeFileTransferAfterAllRecipientsConfirmed:
      body.purgeFileTransferAfterAllRecipientsConfirmed ??
      DEFAULTS.purgeFileTransferAfterAllRecipientsConfirmed,
    purgeFileTransferGracePeriod:
      body.purgeFileTransferGracePeriod ?? DEFAULTS.purgeFileTransferGracePeriod,
    requiredParty: body.requiredParty || DEFAULTS.requiredParty,
    approvedForDisabledVirusScan:
      body.approvedForDisabledVirusScan ?? DEFAULTS.approvedForDisabledVirusScan,
  }
}

/**
 * Updates the broker configuration for a resource via ConfigureResource.
 */
export async function configureResource(
  resourceId: string,
  input: ConfigureResourceInput,
  onBehalfOf?: string,
  environment?: string,
): Promise<void> {
  const path = resourcePath(resourceId, environment, onBehalfOf)

  await apiFetch<void>(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
    redirectOnUnauthorized: false,
  })
}

function resourcePath(resourceId: string, environment?: string, onBehalfOf?: string): string {
  const params = new URLSearchParams()
  if (onBehalfOf) {
    params.set('onBehalfOf', onBehalfOf)
  }
  if (environment) {
    params.set('environment', environment)
  }
  const query = params.toString()
  return `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}${query ? `?${query}` : ''}`
}
