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
  expirationTime: string
}

/** One page of summaries, and how to continue past it. */
export type FileTransferSummaryPage = {
  items: FileTransferSummary[]
  /**
   * Pass back to read the next page. Null on the last one — which is also how the list knows
   * there is nothing more, so the API's `hasNextPage` is not needed here.
   */
  continuationToken: string | null
}

/** Reads one page of summaries, continuing from `continuationToken` when given. */
export function fetchSummaryPage(
  path: string,
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
): Promise<FileTransferSummaryPage> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
  if (continuationToken) params.set('continuationToken', continuationToken)

  return apiFetch<FileTransferSummaryPage>(`${path}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
