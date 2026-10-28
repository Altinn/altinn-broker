import { Card, Heading, Paragraph, Tag } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import {
  DialogMetadata,
  Tooltip,
  type DialogMetadataDueAtProps,
  type DialogStatusProps,
} from '@altinn/altinn-components'
import { Link } from 'react-router-dom'
import './FileTransferCard.css'

type FileTransferCardProps = {
  resourceName: string
  isSender: boolean
  sender: string
  recipients: string[]
  currentActorName: string
  reference: string
  status?: DialogStatusProps
  extendedStatusLabel?: string
  deadline?: DialogMetadataDueAtProps
  to: string
}

export function FileTransferCard({
  resourceName,
  isSender,
  sender,
  recipients,
  currentActorName,
  reference,
  status,
  extendedStatusLabel,
  deadline,
  to,
}: FileTransferCardProps) {
  const { t } = useTranslation()
  const otherRecipientCount = recipients.length - 1
  const visibleRecipient = isSender ? (recipients[0] ?? '') : currentActorName
  const hasOtherRecipients = otherRecipientCount > 0
  const otherRecipients = isSender
    ? recipients.slice(1)
    : (() => {
        const selfIndex = recipients.indexOf(currentActorName)
        return selfIndex === -1 ? recipients.slice(1) : recipients.filter((_, index) => index !== selfIndex)
      })()

  return (
    <Link to={to} className="file-transfer-card-link">
      <Card className="file-transfer-card">
        <div className="file-transfer-card__content">
          <div className="file-transfer-card__heading-row">
            <Heading level={3} data-size="xs">
              {resourceName}
            </Heading>
            <Tag data-color={isSender ? 'accent' : 'neutral'} data-size="sm">
              {isSender ? t('page.common.sender') : t('page.common.recipient')}
            </Tag>
          </div>
          <Paragraph data-size="sm" className="file-transfer-card__party">
            {t('page.list.sender_line', { name: sender })}
          </Paragraph>
          <Paragraph data-size="sm" className="file-transfer-card__party">
            {t('page.list.recipient_line', { name: visibleRecipient })}
            {hasOtherRecipients && (
              <Tooltip content={otherRecipients.join('\n')}>
                <span className="file-transfer-card__counterparty-extra" tabIndex={0}>
                  {otherRecipientCount === 1
                    ? t('page.list.other_one', { count: otherRecipientCount })
                    : t('page.list.other_many', { count: otherRecipientCount })}
                </span>
              </Tooltip>
            )}
          </Paragraph>
          <Paragraph data-size="sm" className="file-transfer-card__reference">
            {t('page.list.reference_line', { reference })}
          </Paragraph>
          {(status || deadline) && (
            <DialogMetadata
              className="file-transfer-card__metadata"
              status={status}
              extendedStatusLabel={extendedStatusLabel}
              dueAt={deadline}
            />
          )}
        </div>
        <span className="file-transfer-card__chevron" aria-hidden="true">
          ›
        </span>
      </Card>
    </Link>
  )
}
