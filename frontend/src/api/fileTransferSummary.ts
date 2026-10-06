import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'

/** Lean summary of a file transfer, shared by the active and historical list endpoints. */
export type FileTransferSummary = {
  fileTransferId: string
  resourceId: string
  sendersFileTransferReference: string
  sender: string
  isSender: boolean
  recipients: string[]
  /** When the file transfer reached its current status — what the list is sorted by. */
  sortDate: string
}

/** One page of summaries, and how to continue past it. */
export type FileTransferSummaryPage = {
  items: FileTransferSummary[]
  hasNextPage: boolean
  /** Pass back to read the next page. Null on the last one. */
  continuationToken: string | null
}

/** Which side of the file transfer the party was on. */
export type FileTransferRole = 'Both' | 'Sender' | 'Recipient'

/** Shorter terms match too much to be worth a round trip; the API ignores them anyway. */
export const MIN_SEARCH_LENGTH = 3

/** Reads one page of summaries, continuing from `continuationToken` when given. */
export function fetchSummaryPage(
  path: string,
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
  search: string | undefined,
  role: FileTransferRole,
): Promise<FileTransferSummaryPage> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
  if (continuationToken) params.set('continuationToken', continuationToken)
  if (search) params.set('search', search)
  if (role !== 'Both') params.set('role', role)

  return apiFetch<FileTransferSummaryPage>(`${path}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
