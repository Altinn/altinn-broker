import type { DialogMetadataDueAtProps } from '@altinn/altinn-components'
import { formatDateTime } from './dateTimeHelper'

const SOON_THRESHOLD_DAYS = 14
const MS_PER_DAY = 1000 * 60 * 60 * 24

/**
 * Shown the way arbeidsflate shows a dialog's due date: urgent while the viewer still has to act,
 * neutral while the transfer is only waiting on others.
 */
export function getDeadlineProps(expirationTime: string, waitingOnOthers: boolean): DialogMetadataDueAtProps | undefined {
  const formatted = formatDateTime(expirationTime)
  if (!formatted) {
    return undefined
  }

  const remaining = new Date(expirationTime).getTime() - Date.now()
  const label = `${remaining < 0 ? 'Frist utgått' : 'Frist'}: ${formatted}`

  if (waitingOnOthers) {
    return { datetime: expirationTime, label, color: 'neutral', variant: 'outline' }
  }

  return { datetime: expirationTime, label, color: remaining < SOON_THRESHOLD_DAYS * MS_PER_DAY ? 'danger' : 'warning' }
}
