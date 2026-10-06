import type { SelectedParty } from '../parties/PartiesContext'
import { BROKER_API_PREFIX } from './config'
import { fetchSummaryPage, type FileTransferRole, type FileTransferSummaryPage } from './fileTransferSummary'

const ACTIVE_FILETRANSFERS_PATH = `${BROKER_API_PREFIX}/frontend/active-file-transfers`

/**
 * Active (published) file transfers across the given resources, newest first and capped at a page.
 * Continue past it with the token from the previous page.
 */
export function getActiveFileTransfers(
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
  search: string | undefined,
  role: FileTransferRole,
): Promise<FileTransferSummaryPage> {
  return fetchSummaryPage(ACTIVE_FILETRANSFERS_PATH, resourceIds, onBehalfOf, continuationToken, search, role)
}
