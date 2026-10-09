import type { SelectedParty } from '../parties/PartiesContext'
import { BROKER_API_PREFIX } from './config'
import { fetchSummaryPage, type FileTransferSummaryPage } from './fileTransferSummary'

const HISTORICAL_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/historical-file-transfers`

/**
 * Historical (cancelled, purged, failed or fully confirmed downloaded) file transfers, newest first
 * and capped at a page. Continue past it with the token from the previous page.
 */
export function getHistoricalFileTransfers(
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
): Promise<FileTransferSummaryPage> {
  return fetchSummaryPage(HISTORICAL_FILETRANSFERS_PATH, resourceIds, onBehalfOf, continuationToken)
}
