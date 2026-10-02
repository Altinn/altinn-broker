import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import { dateRangeParams, type DateRange, type FileTransferSummaryPage } from './fileTransferSummary'

const ACTIVE_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/active-file-transfers`

/**
 * Lean summary (fileTransferId, sender, recipients, reference) of active (published) file
 * transfers across the given resources, fetched in a single backend call.
 *
 * The API returns at most a page of them, newest first. Narrow with `range` to reach older ones.
 */
export function getActiveFileTransfers(
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
  range: DateRange = {},
): Promise<FileTransferSummaryPage> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
  Object.entries(dateRangeParams(range)).forEach(([key, value]) => params.set(key, value))
  return apiFetch<FileTransferSummaryPage>(`${ACTIVE_FILETRANSFERS_PATH}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
