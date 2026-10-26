import { intlLocale } from '../i18n/config'

/** "9. oktober 2026 kl. 14:30" in the viewer's time zone. Undefined for anything it cannot read. */
export function formatDateTime(isoDateTime: string | null | undefined): string | undefined {
  if (!isoDateTime) {
    return undefined
  }

  const date = new Date(isoDateTime)
  if (Number.isNaN(date.getTime())) {
    return undefined
  }

  return new Intl.DateTimeFormat(intlLocale(), { dateStyle: 'long', timeStyle: 'short' }).format(date)
}
