import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import type { FileTransferSummary } from './fileTransferSummary'

const ACTIVE_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/active-file-transfers`

/**
 * Lean summary (fileTransferId, sender, recipients, reference) of active (published) file
 * transfers across the given resources, fetched in a single backend call.
 */
export function getActiveFileTransfers(
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
): Promise<FileTransferSummary[]> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
  return apiFetch<FileTransferSummary[]>(`${ACTIVE_FILETRANSFERS_PATH}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
