import type { SelectedParty } from '../parties/PartiesContext'
import { BROKER_API_PREFIX } from './config'
import { fetchSummaryPage, type FileTransferSummaryPage } from './fileTransferSummary'

const DRAFT_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/draft-file-transfers`

/**
 * The sender's own file transfers whose file never finished uploading, across the given resources,
 * newest first and capped at a page. Continue past it with the token from the previous page.
 */
export function getDraftFileTransfers(
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
): Promise<FileTransferSummaryPage> {
  return fetchSummaryPage(DRAFT_FILETRANSFERS_PATH, resourceIds, onBehalfOf, continuationToken)
}
