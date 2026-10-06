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
}

/**
 * One page of summaries. The list endpoints cap how much they return, so `hasMore` is what lets the
 * page say "there are older ones" instead of letting a capped list pass for a complete one.
 */
export type FileTransferSummaryPage = {
  items: FileTransferSummary[]
  hasMore: boolean
  pageSize: number
}

/** Reads one page of summaries from a list endpoint. */
export function fetchSummaryPage(
  path: string,
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
): Promise<FileTransferSummaryPage> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)

  return apiFetch<FileTransferSummaryPage>(`${path}?${params}`, {
    redirectOnUnauthorized: false,
  })
}
