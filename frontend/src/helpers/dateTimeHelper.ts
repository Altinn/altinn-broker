const DATE_TIME_FORMAT = new Intl.DateTimeFormat('nb-NO', { dateStyle: 'long', timeStyle: 'short' })

/** "9. oktober 2026 kl. 14:30" in the viewer's time zone. Undefined for anything it cannot read. */
export function formatDateTime(isoDateTime: string | null | undefined): string | undefined {
  if (!isoDateTime) {
    return undefined
  }

  const date = new Date(isoDateTime)
  return Number.isNaN(date.getTime()) ? undefined : DATE_TIME_FORMAT.format(date)
}
