import { ListItem } from '@altinn/altinn-components'
import { Paragraph, Tag } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import type { FileTransferDetails } from '../../api/fileTransferDetail'
import { formatOrganizationDisplay } from '../../helpers/orgIdentifierHelper'
import {
  formatCustomRecipientCount,
  formatReminderStatus,
  getNotificationStatusDisplay,
} from '../../helpers/notificationStatusHelper'
import './NotificationDetail.css'

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

  const notifiedRecipients = (transferDetails.recipients ?? []).filter((recipient) => recipient.notification)
  if (notifiedRecipients.length === 0) {
    return <>{t('page.detail.notification_ordered')}</>
  }

  const isPublished = Boolean(transferDetails.published)
  return (
    <ul className="notification-detail">
      {notifiedRecipients.map(({ recipient, recipientName, notification }) => {
        if (!notification) return null
        const status = getNotificationStatusDisplay(notification, isPublished, t)
        const customRecipients = formatCustomRecipientCount(notification, t)
        return (
          <li key={recipient} className="notification-detail__recipient">
            <div className="notification-detail__heading">
              <Paragraph data-size="sm">{formatOrganizationDisplay(recipientName ?? recipient, recipient)}</Paragraph>
              <Tag data-color={status.color} data-size="sm">
                {status.label}
              </Tag>
            </div>
            {notification.reminder && <Paragraph data-size="sm">{formatReminderStatus(notification.reminder, t)}</Paragraph>}
            {customRecipients && <Paragraph data-size="sm">{customRecipients}</Paragraph>}
          </li>
        )
      })}
    </ul>
  )
}
