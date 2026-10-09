import { Card, Heading, Paragraph, Tag } from '@digdir/designsystemet-react'
import { Tooltip } from '@altinn/altinn-components'
import { Link } from 'react-router-dom'
import './FileTransferCard.css'

type FileTransferCardProps = {
  resourceName: string
  isSender: boolean
  sender: string
  recipients: string[]
  currentActorName: string
  reference: string
  to: string
}

export function FileTransferCard({
  resourceName,
  isSender,
  sender,
  recipients,
  currentActorName,
  reference,
  to,
}: FileTransferCardProps) {
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
              {isSender ? 'Avsender' : 'Mottaker'}
            </Tag>
          </div>
          <Paragraph data-size="sm" className="file-transfer-card__party">
            Avsender: {sender}
          </Paragraph>
          <Paragraph data-size="sm" className="file-transfer-card__party">
            Mottaker: {visibleRecipient}
            {hasOtherRecipients && (
              <Tooltip content={otherRecipients.join('\n')}>
                <span className="file-transfer-card__counterparty-extra" tabIndex={0}>
                  (+{otherRecipientCount} {otherRecipientCount === 1 ? 'annen' : 'andre'})
                </span>
              </Tooltip>
            )}
          </Paragraph>
          <Paragraph data-size="sm" className="file-transfer-card__reference">
            Referanse: {reference}
          </Paragraph>
        </div>
        <span className="file-transfer-card__chevron" aria-hidden="true">
          ›
        </span>
      </Card>
    </Link>
  )
}
