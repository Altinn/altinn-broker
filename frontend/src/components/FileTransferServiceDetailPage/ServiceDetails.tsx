import { Button, ButtonIcon, ButtonLabel, Heading, List, ResourceListItem } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { ArrowUndoIcon, PlusIcon } from '@navikt/aksel-icons'
import { Link, type LinkProps } from 'react-router-dom'
import type { AllowedRecipient } from '../../api/allowedRecipients'
import { newFileTransferPath, PageRoutes } from '../../pages/routes'
import type { FileTransferService } from './useFileTransferService'
import { CannotSendNotice } from './CannotSendNotice'
import { NoRecipientsNotice } from './NoRecipientsNotice'
import { ResourceConfigurationList } from './ResourceConfigurationList'
import './fileTransferServiceDetailPage.css'

/** Fills the `as` slot altinn-components exposes, so the button renders as a router link. */
const linkTo = (to: string) => (props: LinkProps) => <Link {...props} to={to} />

/** The resource, and the broker configuration every file transfer on it follows. */
export function ServiceDetails({
  resource,
  configuration,
  allowedRecipients,
  accessListCandidates,
  onBehalfOf,
  sender,
}: FileTransferService & {
  onBehalfOf: string
  sender: AllowedRecipient
}) {
  const { t } = useTranslation()
  // Without anyone to send to there is nothing to create, so the action is withheld rather
  // than leading the user into a form they cannot complete.
  const hasRecipients = allowedRecipients.length > 0

  return (
    <div className="page">
      <nav className="service-detail__back">
        <Button size="sm" variant="ghost" as={linkTo(PageRoutes.services)}>
          <ButtonIcon icon={ArrowUndoIcon} />
          <ButtonLabel>{t('page.common.back')}</ButtonLabel>
        </Button>
      </nav>

      <section className="page-section">
        <Heading as="h1" size="md" className="service-detail__heading">
          {t('page.service.title')}
        </Heading>
        <List className="service-detail__resource">
          <ResourceListItem
            id={resource.resourceId}
            resourceName={resource.name ?? resource.resourceId}
            ownerName={resource.serviceOwnerName ?? t('page.common.unknown_owner')}
            description={resource.serviceOwnerName ? t('page.common.owned_by', { name: resource.serviceOwnerName }) : undefined}
            titleAs="h2"
            interactive={false}
            shadow="none"
            border="none"
          />
        </List>
      </section>

      {resource.canSend && hasRecipients && (
        <div className="page-actions page-section">
          <Button as={linkTo(newFileTransferPath(resource.resourceId))}>
            <ButtonIcon icon={PlusIcon} />
            <ButtonLabel>{t('page.service.create')}</ButtonLabel>
          </Button>
        </div>
      )}

      {resource.canSend && !hasRecipients && (
        <div className="page-section">
          <NoRecipientsNotice />
        </div>
      )}

      {!resource.canSend && (
        <div className="page-section">
          <CannotSendNotice />
        </div>
      )}

      <section className="page-section">
        <Heading as="h2" size="sm" className="service-detail__heading">
          {t('page.service.settings')}
        </Heading>
        <ResourceConfigurationList
          resourceId={resource.resourceId}
          configuration={configuration}
          onBehalfOf={onBehalfOf}
          canPublish={resource.canPublish}
          isServiceOwner={resource.isServiceOwner}
          sender={sender}
          accessListCandidates={accessListCandidates}
        />
      </section>
    </div>
  )
}

export function LoadingServiceDetails() {
  const { t } = useTranslation()
  return (
    <div className="page">
      <section className="page-section">
        <Heading as="h1" size="md" className="service-detail__heading">
          {t('page.service.title')}
        </Heading>
        <List className="service-detail__resource">
          <ResourceListItem
            id="laster-formidlingstjeneste"
            resourceName={t('page.services.loading_name')}
            ownerName={t('page.services.loading_owner')}
            titleAs="h2"
            loading
            shadow="none"
            border="none"
          />
        </List>
      </section>
    </div>
  )
}
