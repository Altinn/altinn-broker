import type { SelectedParty } from '../parties/PartiesContext'
import { BROKER_API_PREFIX } from './config'
import { fetchSummaryPage, type DateRange, type FileTransferSummaryPage } from './fileTransferSummary'

const HISTORICAL_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/historical-file-transfers`

/**
 * Historical (cancelled, purged, failed or fully confirmed downloaded) file transfers, newest first
 * and capped at a page. A party that has used the service for a while has far more than fits.
 */
export function getHistoricalFileTransfers(
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
  range?: DateRange,
): Promise<FileTransferSummaryPage> {
  return fetchSummaryPage(HISTORICAL_FILETRANSFERS_PATH, resourceIds, onBehalfOf, range)
}
