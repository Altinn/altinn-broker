import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import { dateRangeParams, type DateRange, type FileTransferSummaryPage } from './fileTransferSummary'

const HISTORICAL_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/historical-file-transfers`

/**
 * Lean summary (fileTransferId, sender, recipients, reference) of historical (past Published -
 * cancelled, purged, failed, or fully confirmed downloaded) file transfers across the given
 * resources, fetched in a single backend call.
 *
 * The API returns at most a page of them, newest first. Narrow with `range` to reach older ones —
 * a party that has been using the service for a while has far more history than fits in one page.
 */
export function getHistoricalFileTransfers(
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
  range: DateRange = {},
): Promise<FileTransferSummaryPage> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
  Object.entries(dateRangeParams(range)).forEach(([key, value]) => params.set(key, value))
  return apiFetch<FileTransferSummaryPage>(`${HISTORICAL_FILETRANSFERS_PATH}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
