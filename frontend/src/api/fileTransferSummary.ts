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

/** The dates the user picked, as `YYYY-MM-DD`. Either end may be left open. */
export type DateRange = {
  from?: string
  to?: string
}

/**
 * Reads one page of summaries from a list endpoint.
 *
 * The date boundaries are the user's own day: picking 5 October means everything that happened on
 * 5 October where they are, not where the server is.
 */
export function fetchSummaryPage(
  path: string,
  resourceIds: string[],
  onBehalfOf?: SelectedParty,
  range: DateRange = {},
): Promise<FileTransferSummaryPage> {
  const params = new URLSearchParams()
  resourceIds.forEach((resourceId) => params.append('resourceIds', resourceId))
  if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)

  const from = instant(range.from, '00:00:00.000')
  const to = instant(range.to, '23:59:59.999')
  if (from) params.set('from', from)
  if (to) params.set('to', to)

  return apiFetch<FileTransferSummaryPage>(`${path}?${params}`, {
    redirectOnUnauthorized: false,
  })
}

function instant(date: string | undefined, time: string): string | undefined {
  if (!date) {
    return undefined
  }
  // No zone designator, so this is read as local time — which is the point.
  const parsed = new Date(`${date}T${time}`)
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString()
}
