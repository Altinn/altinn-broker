import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import type { FileTransferSummary } from './fileTransferSummary'

const HISTORICAL_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/historical-file-transfers`

/**
 * Lean summary (fileTransferId, sender, recipients, reference) of historical (past Published -
 * cancelled, purged, failed, or fully confirmed downloaded) file transfers across the given
 * resources, fetched in a single backend call.
 */
export function getHistoricalFileTransfers(
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
): Promise<FileTransferSummary[]> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
  return apiFetch<FileTransferSummary[]>(`${HISTORICAL_FILETRANSFERS_PATH}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
