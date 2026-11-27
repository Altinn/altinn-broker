import { Paragraph, Tag } from '@digdir/designsystemet-react'
import { formatOrganizationDisplay } from '../../helpers/orgIdentifierHelper'
import './RecipientStatusList.css'

export type RecipientStatusTag = {
  label: string
  color: 'success' | 'danger' | 'warning' | 'info' | 'neutral'
}

export type RecipientStatus = {
  recipient: string
  recipientName?: string
  tag: RecipientStatusTag
  details?: (string | undefined)[]
}

type RecipientStatusListProps = {
  statuses: RecipientStatus[]
}

/** One entry per recipient: its name, a tag for where it stands, and any detail lines below. */
export function RecipientStatusList({ statuses }: RecipientStatusListProps) {
  return (
    <ul className="recipient-status-list">
      {statuses.map(({ recipient, recipientName, tag, details = [] }) => (
        <li key={recipient} className="recipient-status-list__recipient">
          <div className="recipient-status-list__heading">
            <Paragraph data-size="sm">{formatOrganizationDisplay(recipientName ?? recipient, recipient)}</Paragraph>
            <Tag data-color={tag.color} data-size="sm">
              {tag.label}
            </Tag>
          </div>
          {details
            .filter((detail): detail is string => Boolean(detail))
            .map((detail) => (
              <Paragraph key={detail} data-size="sm">
                {detail}
              </Paragraph>
            ))}
        </li>
      ))}
    </ul>
  )
}
