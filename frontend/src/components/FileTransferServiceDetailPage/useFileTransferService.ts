import { useEffect, useState } from 'react'
import { getAllowedRecipients, type AllowedRecipient } from '../../api/allowedRecipients'
import { getResourceConfiguration, type ResourceConfiguration } from '../../api/resourceConfiguration'
import { fetchAuthorizedResources, type AuthorizedResource } from '../../api/resources'

export type FileTransferService = {
  resource: AuthorizedResource
  configuration: ResourceConfiguration
  /** Empty means no one may receive on the resource, so no file transfer can be created. */
  allowedRecipients: AllowedRecipient[]
}

export type FileTransferServiceState =
  | { status: 'loaded'; service: FileTransferService }
  | { status: 'missing' }
  | { status: 'failed' }

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

async function loadService(resourceId: string, party: string): Promise<FileTransferServiceState> {
  try {
    const [resources, configuration, allowedRecipients] = await Promise.all([
      fetchAuthorizedResources(party),
      getResourceConfiguration(resourceId),
      getAllowedRecipients(resourceId, party),
    ])

    const resource = resources.find((candidate) => candidate.resourceId === resourceId)
    return resource
      ? { status: 'loaded', service: { resource, configuration, allowedRecipients } }
      : { status: 'missing' }
  } catch {
    return { status: 'failed' }
  }
}
