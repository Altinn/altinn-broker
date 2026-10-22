import type { NotificationReminderDetail, RecipientNotificationDetail } from '../api/fileTransferDetail'

export type NotificationTagColor = 'success' | 'danger' | 'warning' | 'info' | 'neutral'

export type NotificationStatusDisplay = {
  label: string
  color: NotificationTagColor
}

/**
 * How far the notification to one recipient has come. A notification is only ordered once the file is published,
 * so a pending one on an unpublished transfer is simply waiting for the file.
 */
export function getNotificationStatusDisplay(
  notification: RecipientNotificationDetail,
  isPublished: boolean,
): NotificationStatusDisplay {
  switch (notification.status) {
    case 'Pending':
      return isPublished
        ? { label: 'Varsel bestilles', color: 'info' }
        : { label: 'Varsel sendes når filen er klar', color: 'neutral' }
    case 'Ordered':
      return { label: 'Varsel er bestilt – utsendelse ikke bekreftet', color: 'info' }
    case 'Sent':
      return { label: notification.sentTime ? `Varslet ${notification.sentTime}` : 'Varslet', color: 'success' }
    case 'Rejected':
    case 'NotDelivered':
      return { label: 'Kunne ikke varsles', color: 'danger' }
    case 'ConditionNotMet':
      return { label: 'Varsel ikke sendt', color: 'neutral' }
    case 'OrderUnconfirmed':
      return { label: 'Utsendelse kunne ikke bekreftes', color: 'warning' }
  }
}

export function formatReminderStatus(reminder: NotificationReminderDetail): string {
  switch (reminder.status) {
    case 'Pending':
    case 'Ordered':
      return reminder.time ? `Påminnelse planlagt ${reminder.time}` : 'Påminnelse planlagt'
    case 'Sent':
      return reminder.time ? `Påminnelse sendt ${reminder.time}` : 'Påminnelse sendt'
    case 'Rejected':
    case 'NotDelivered':
      return 'Påminnelse kunne ikke sendes'
    case 'ConditionNotMet':
      return 'Påminnelse ikke sendt'
    case 'OrderUnconfirmed':
      return 'Utsendelse av påminnelse kunne ikke bekreftes'
  }
}

export function formatCustomRecipientCount(notification: RecipientNotificationDetail): string | undefined {
  const { customRecipientCount: count, customRecipientsNotifiedCount: notified } = notification
  if (count === 0) return undefined
  return `Andre varslingsmottakere: ${notified} av ${count} varslet`
}
