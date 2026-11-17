import { ListItem } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import type { FileTransferDetails } from '../../api/fileTransferDetail'
import {
  formatCustomRecipientCount,
  formatReminderStatus,
  getNotificationStatusDisplay,
} from '../../helpers/notificationStatusHelper'
import { RecipientStatusList, type RecipientStatus } from './RecipientStatusList'

type NotificationDetailProps = {
  transferDetails: FileTransferDetails
}

/**
 * The "Varsling" row of the detail list. Unlike a DetailField its value is not plain text: one line per recipient
 * whose notification the viewer may see - every recipient for the sender, only its own for a recipient - with a tag
 * for how far it has come, the reminder, and how many custom recipients were notified.
 */
export function NotificationDetail({ transferDetails }: NotificationDetailProps) {
  const { t } = useTranslation()
  return (
    <ListItem
      className="data-list-item"
      shadow="none"
      interactive={false}
      title={t('page.detail.notification')}
      description={<NotificationDescription transferDetails={transferDetails} />}
    />
  )
}

function NotificationDescription({ transferDetails }: NotificationDetailProps) {
  const { t } = useTranslation()
  if (!transferDetails.hasNotification) {
    return <>{t('page.detail.notification_not_ordered')}</>
  }

  const isPublished = Boolean(transferDetails.published)
  const statuses: RecipientStatus[] = (transferDetails.recipients ?? []).flatMap(({ recipient, recipientName, notification }) =>
    notification
      ? [{
          recipient,
          recipientName,
          tag: getNotificationStatusDisplay(notification, isPublished, t),
          details: [
            notification.reminder ? formatReminderStatus(notification.reminder, t) : undefined,
            formatCustomRecipientCount(notification, t),
          ],
        }]
      : [],
  )
  if (statuses.length === 0) {
    return <>{t('page.detail.notification_ordered')}</>
  }

  return <RecipientStatusList statuses={statuses} />
}
