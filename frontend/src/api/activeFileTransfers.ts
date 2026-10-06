import type { SelectedParty } from '../parties/PartiesContext'
import { BROKER_API_PREFIX } from './config'
import { fetchSummaryPage, type DateRange, type FileTransferSummaryPage } from './fileTransferSummary'

const ACTIVE_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/active-file-transfers`

/**
 * Active (published) file transfers across the given resources, newest first and capped at a page.
 * Narrow with `range` to reach older ones.
 */
export function getActiveFileTransfers(
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
  range?: DateRange,
): Promise<FileTransferSummaryPage> {
  return fetchSummaryPage(ACTIVE_FILETRANSFERS_PATH, resourceIds, onBehalfOf, range)
}
