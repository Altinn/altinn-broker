import { Card, Heading, Paragraph, Tag } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import type { AuthorizedResource } from '../../api/resources'
import { servicePath } from '../../pages/routes'
// Same card as in the file transfer lists, so the styles are shared rather than copied.
import '../FileTransfersPages/FileTransferCard.css'
import './FileTransferServiceCard.css'

type FileTransferServiceCardProps = {
  service: AuthorizedResource
  /** The service owner is not provisioned, or its status is unknown, so the service cannot be opened. */
  locked?: boolean
}

/** A formidlingstjeneste, tagged with what the selected party can do with it. */
export function FileTransferServiceCard({ service, locked = false }: FileTransferServiceCardProps) {
  const { t } = useTranslation()
  const owner = service.serviceOwnerName
    ? t('page.common.owned_by', { name: service.serviceOwnerName })
    : t('page.common.unknown_owner')

  const content = (
    <>
      <div className="file-transfer-card__content">
        <div className="file-transfer-card__heading-row">
          <Heading level={2} data-size="xs" className="file-transfer-card__title">
            {service.name ?? service.resourceId}
          </Heading>
          <div className="file-transfer-service-card__tags">
            {service.isOwned && (
              <Tag data-color="accent" data-size="sm">
                {t('page.services.service_owner')}
              </Tag>
            )}
            {service.canSend && (
              <Tag data-color="accent" data-size="sm">
                {t('page.services.can_send')}
              </Tag>
            )}
            {service.canReceive && (
              <Tag data-color="neutral" data-size="sm">
                {t('page.services.can_receive')}
              </Tag>
            )}
          </div>
        </div>
        <Paragraph data-size="sm" className="file-transfer-card__party">
          {locked ? t('page.services.provision_pending_resource') : owner}
        </Paragraph>
      </div>
      <span className="file-transfer-card__chevron" aria-hidden="true">
        ›
      </span>
    </>
  )

  if (locked) {
    return (
      <Card className="file-transfer-card file-transfer-service-card file-transfer-service-card--locked">
        {content}
      </Card>
    )
  }

  return (
    <Link to={servicePath(service.resourceId)} className="file-transfer-card-link">
      <Card className="file-transfer-card file-transfer-service-card">{content}</Card>
    </Link>
  )
}
