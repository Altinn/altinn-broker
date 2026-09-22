import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const RESOURCE_PATH = `${BROKER_API_PREFIX}/resource`
const VIRUS_SCAN_MAX_SIZE = 50 * 1000 * 1000 * 1000

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

/** An unapproved resource cannot turn off virus scanning, so the scan cap bounds every transfer on it. */
function enforcedMaxFileTransferSize(configured: number | null, approvedForDisabledVirusScan: boolean) {
  if (approvedForDisabledVirusScan) {
    return configured
  }

  return configured === null ? VIRUS_SCAN_MAX_SIZE : Math.min(configured, VIRUS_SCAN_MAX_SIZE)
}

/**
 * Reads the broker configuration for a resource.
 */
export async function getResourceConfiguration(resourceId: string): Promise<ResourceConfiguration> {
  const body = await apiFetch<ResourceConfigurationBody>(
    `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}`,
    { redirectOnUnauthorized: false },
  )

  const approvedForDisabledVirusScan =
    body.approvedForDisabledVirusScan ?? DEFAULTS.approvedForDisabledVirusScan

  return {
    maxFileTransferSize: enforcedMaxFileTransferSize(
      body.maxFileTransferSize ?? DEFAULTS.maxFileTransferSize,
      approvedForDisabledVirusScan,
    ),
    fileTransferTimeToLive: body.fileTransferTimeToLive ?? DEFAULTS.fileTransferTimeToLive,
    purgeFileTransferAfterAllRecipientsConfirmed:
      body.purgeFileTransferAfterAllRecipientsConfirmed ??
      DEFAULTS.purgeFileTransferAfterAllRecipientsConfirmed,
    purgeFileTransferGracePeriod:
      body.purgeFileTransferGracePeriod ?? DEFAULTS.purgeFileTransferGracePeriod,
    requiredParty: body.requiredParty || DEFAULTS.requiredParty,
    approvedForDisabledVirusScan,
  }
}
