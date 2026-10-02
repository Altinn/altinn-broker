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
 * Turns the picked dates into the instants the API filters on.
 *
 * The boundaries are the user's own day: picking 5 October means everything that happened on
 * 5 October where they are, not where the server is.
 */
export function dateRangeParams(range: DateRange): Record<string, string> {
  const params: Record<string, string> = {}
  const from = instant(range.from, '00:00:00.000')
  const to = instant(range.to, '23:59:59.999')

  if (from) params.from = from
  if (to) params.to = to
  return params
}

function instant(date: string | undefined, time: string): string | undefined {
  if (!date) {
    return undefined
  }
  // No zone designator, so this is read as local time — which is the point.
  const parsed = new Date(`${date}T${time}`)
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString()
}
