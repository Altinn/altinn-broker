import { Card, Heading, Paragraph } from '@digdir/designsystemet-react'
import { Link } from 'react-router-dom'
import './ActiveFileTransferCard.css'

type ActiveFileTransferCardProps = {
  resourceName: string
  sender: string
  recipient: string
  reference: string
  to: string
}

export function ActiveFileTransferCard({
  resourceName,
  sender,
  recipient,
  reference,
  to,
}: ActiveFileTransferCardProps) {
  return (
    <Link to={to} className="active-file-transfer-card-link">
      <Card className="active-file-transfer-card">
        <div className="active-file-transfer-card__content">
          <Heading level={3} data-size="xs">
            {resourceName}
          </Heading>
          <Paragraph data-size="sm">
            {sender} → {recipient} - {reference}
          </Paragraph>
        </div>
        <span className="active-file-transfer-card__chevron" aria-hidden="true">
          ›
        </span>
      </Card>
    </Link>
  )
}
