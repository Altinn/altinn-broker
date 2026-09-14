import { formatOrgNumber } from '../helpers/orgIdentifierHelper'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const RESOURCE_PATH = `${BROKER_API_PREFIX}/resource`

/** An organization the sending party may address a file transfer to on a resource. */
export type AllowedRecipient = {
  organizationNumber: string
  name: string
}

/** What the API returns; the name is null when Altinn Register had no name for the organization. */
type AllowedRecipientBody = {
  organizationNumber: string
  name: string | null
}

/**
 * Reads the organizations the party may send to on a resource.
 *
 * The API has already applied the resource's access list and required party and removed the sender,
 * so the list is used as it stands. An empty list means the resource has no access list, and any
 * organization may receive.
 */
export async function getAllowedRecipients(
  resourceId: string,
  party: string,
): Promise<AllowedRecipient[]> {
  const query = new URLSearchParams({ party })
  const path = `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}/allowed-recipients?${query}`

  try {
    const body = await apiFetch<AllowedRecipientBody[]>(path)
    if (import.meta.env.DEV) {
      console.log('[allowedRecipients] GET %s ->', path, body)
    }
    return (body ?? []).map(toRecipient)
  } catch (error) {
    // The form loads this alongside the resource configuration, so a failure here is otherwise
    // indistinguishable from that one failing.
    if (import.meta.env.DEV) {
      console.log('[allowedRecipients] GET %s failed ->', path, error)
    }
    throw error
  }
}

/** The picker shows a name for every option, so an unnamed organization falls back to its number. */
function toRecipient({ organizationNumber, name }: AllowedRecipientBody): AllowedRecipient {
  return { organizationNumber, name: name ?? formatOrgNumber(organizationNumber) }
}
