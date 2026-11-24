import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { getAllowedRecipients, type AllowedRecipient } from '../../api/allowedRecipients'
import { getResourceConfiguration, type ResourceConfiguration } from '../../api/resourceConfiguration'
import { fetchAuthorizedResources, type AuthorizedResource } from '../../api/resources'
import { localizeApiError } from '../../i18n/apiErrors'

/** GetResource when the resource's service owner has no Broker row yet. */
const SERVICE_OWNER_NOT_CONFIGURED = 24

export type FileTransferService = {
  resource: AuthorizedResource
  configuration: ResourceConfiguration
  /** Empty means no one may receive on the resource, so no file transfer can be created. */
  allowedRecipients: AllowedRecipient[]
  /**
   * Access-list parties (minus the caller), not narrowed by the configured required party.
   * Used by the configuration editor's required-party dropdown.
   */
  accessListCandidates: AllowedRecipient[]
}

export type FileTransferServiceState =
  | { status: 'loaded'; service: FileTransferService }
  | { status: 'missing' }
  | { status: 'failed' }
  | { status: 'ownerNotConfigured'; resource: AuthorizedResource | null }

/**
 * The resource as the party sees it, together with its broker configuration.
 * Null while loading, and while anything loaded belongs to a previous route or party.
 */
export function useFileTransferService(
  resourceId: string,
  party: string | undefined,
): FileTransferServiceState | null {
  const [loaded, setLoaded] = useState<{ key: string; state: FileTransferServiceState } | null>(null)
  const key = `${party}:${resourceId}`

  useEffect(() => {
    if (!party) {
      return
    }

    let active = true
    loadService(resourceId, party).then((state) => {
      if (active) {
        setLoaded({ key, state })
      }
    })

    return () => {
      active = false
    }
  }, [key, party, resourceId])

  return loaded?.key === key ? loaded.state : null
}

function isServiceOwnerNotConfigured(error: unknown): boolean {
  return error instanceof ApiError && localizeApiError(error.body).errorCode === SERVICE_OWNER_NOT_CONFIGURED
}

async function loadService(resourceId: string, party: string): Promise<FileTransferServiceState> {
  let resources: AuthorizedResource[]
  try {
    resources = await fetchAuthorizedResources(party)
  } catch {
    return { status: 'failed' }
  }

  const resource = resources.find((candidate) => candidate.resourceId === resourceId) ?? null

  try {
    const [configuration, allowedRecipients, accessListCandidates] = await Promise.all([
      getResourceConfiguration(resourceId),
      getAllowedRecipients(resourceId, party),
      getAllowedRecipients(resourceId, party, { ignoreRequiredParty: true }),
    ])

    return resource
      ? {
          status: 'loaded',
          service: { resource, configuration, allowedRecipients, accessListCandidates },
        }
      : { status: 'missing' }
  } catch (error) {
    if (isServiceOwnerNotConfigured(error)) {
      return { status: 'ownerNotConfigured', resource }
    }
    return { status: 'failed' }
  }
}
