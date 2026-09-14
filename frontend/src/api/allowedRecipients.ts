import { formatOrgNumber } from '../helpers/orgIdentifierHelper'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const RESOURCE_PATH = `${BROKER_API_PREFIX}/resource`

/** An organization the sending party may address a file transfer to on a resource. */
export type AllowedRecipient = {
  organizationNumber: string
  name: string
}

type AllowedRecipientBody = {
  organizationNumber: string
  name: string | null
}

/**
 * Reads the organizations the party may send to on a resource.
 */
export async function getAllowedRecipients(
  resourceId: string,
  party: string,
): Promise<AllowedRecipient[]> {
  const query = new URLSearchParams({ party })
  const path = `${RESOURCE_PATH}/${encodeURIComponent(resourceId)}/allowed-recipients?${query}`

  const body = await apiFetch<AllowedRecipientBody[]>(path)
  return (body ?? []).map(toRecipient)
}

function toRecipient({ organizationNumber, name }: AllowedRecipientBody): AllowedRecipient {
  return { organizationNumber, name: name ?? formatOrgNumber(organizationNumber) }
}
