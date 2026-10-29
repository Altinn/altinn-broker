import type { TFunction } from 'i18next'
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
  t: TFunction,
): NotificationStatusDisplay {
  switch (notification.status) {
    case 'Pending':
      return isPublished
        ? { label: t('page.detail.notification_pending'), color: 'info' }
        : { label: t('page.detail.notification_pending_unpublished'), color: 'neutral' }
    case 'Ordered':
      return { label: t('page.detail.notification_ordered_not_confirmed'), color: 'info' }
    case 'Sent':
      return {
        label: notification.sentTime
          ? t('page.detail.notification_sent_at', { time: notification.sentTime })
          : t('page.detail.notification_sent'),
        color: 'success',
      }
    case 'Rejected':
    case 'NotDelivered':
      return { label: t('page.detail.notification_rejected'), color: 'danger' }
    case 'ConditionNotMet':
      return { label: t('page.detail.notification_condition_not_met'), color: 'neutral' }
    case 'OrderUnconfirmed':
      return { label: t('page.detail.notification_order_unconfirmed'), color: 'warning' }
  }
}

export function formatReminderStatus(reminder: NotificationReminderDetail, t: TFunction): string {
  switch (reminder.status) {
    case 'Pending':
    case 'Ordered':
      return reminder.time
        ? t('page.detail.reminder_scheduled_at', { time: reminder.time })
        : t('page.detail.reminder_scheduled')
    case 'Sent':
      return reminder.time ? t('page.detail.reminder_sent_at', { time: reminder.time }) : t('page.detail.reminder_sent')
    case 'Rejected':
    case 'NotDelivered':
      return t('page.detail.reminder_rejected')
    case 'ConditionNotMet':
      return t('page.detail.reminder_condition_not_met')
    case 'OrderUnconfirmed':
      return t('page.detail.reminder_order_unconfirmed')
  }
}

export function formatCustomRecipientCount(notification: RecipientNotificationDetail, t: TFunction): string | undefined {
  const { customRecipientCount: count, customRecipientsNotifiedCount: notified } = notification
  if (count === 0) return undefined
  return t('page.detail.custom_recipients_notified', { notified, count })
}
