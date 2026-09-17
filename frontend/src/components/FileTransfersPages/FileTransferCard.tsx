import { Card, Heading, Paragraph } from '@digdir/designsystemet-react'
import { Link } from 'react-router-dom'
import './FileTransferCard.css'

type FileTransferCardProps = {
  resourceName: string
  sender: string
  recipient: string
  reference: string
  to: string
}

export function FileTransferCard({
  resourceName,
  sender,
  recipient,
  reference,
  to,
}: FileTransferCardProps) {
  return (
    <Link to={to} className="file-transfer-card-link">
      <Card className="file-transfer-card">
        <div className="file-transfer-card__content">
          <Heading level={3} data-size="xs">
            {resourceName}
          </Heading>
          <Paragraph data-size="sm">
            {sender} → {recipient} - {reference}
          </Paragraph>
        </div>
        <span className="file-transfer-card__chevron" aria-hidden="true">
          ›
        </span>
      </Card>
    </Link>
  )
}
