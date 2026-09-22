import { Button, ButtonIcon, ButtonLabel, Heading, List, ResourceListItem } from '@altinn/altinn-components'
import { ArrowUndoIcon, PlusIcon } from '@navikt/aksel-icons'
import { Link, type LinkProps } from 'react-router-dom'
import { newFileTransferPath, PageRoutes } from '../../pages/routes'
import type { FileTransferService } from './useFileTransferService'
import { ResourceConfigurationList } from './ResourceConfigurationList'
import './fileTransferServiceDetailPage.css'

const UNKNOWN_OWNER = 'Ukjent eier'

/** Fills the `as` slot altinn-components exposes, so the button renders as a router link. */
const linkTo = (to: string) => (props: LinkProps) => <Link {...props} to={to} />

/** The resource, and the broker configuration every file transfer on it follows. */
export function ServiceDetails({ resource, configuration }: FileTransferService) {
  return (
    <div className="page">
      <nav className="service-detail__back">
        <Button size="sm" variant="ghost" as={linkTo(PageRoutes.services)}>
          <ButtonIcon icon={ArrowUndoIcon} />
          <ButtonLabel>Tilbake</ButtonLabel>
        </Button>
      </nav>

      <section className="page-section">
        <Heading as="h1" size="md" className="service-detail__heading">
          Formidlingstjeneste
        </Heading>
        <List className="service-detail__resource">
          <ResourceListItem
            id={resource.resourceId}
            resourceName={resource.name ?? resource.resourceId}
            ownerName={resource.serviceOwnerName ?? UNKNOWN_OWNER}
            description={resource.serviceOwnerName ? `Eid av ${resource.serviceOwnerName}` : undefined}
            titleAs="h2"
            interactive={false}
            shadow="none"
            border="none"
          />
        </List>
      </section>

      {resource.canSend && (
        <div className="page-actions page-section">
          <Button as={linkTo(newFileTransferPath(resource.resourceId))}>
            <ButtonIcon icon={PlusIcon} />
            <ButtonLabel>Opprett ny formidling</ButtonLabel>
          </Button>
        </div>
      )}

      <section className="page-section">
        <Heading as="h2" size="sm" className="service-detail__heading">
          Oppsett for tjenesten
        </Heading>
        <ResourceConfigurationList configuration={configuration} />
      </section>
    </div>
  )
}

export function LoadingServiceDetails() {
  return (
    <div className="page">
      <section className="page-section">
        <Heading as="h1" size="md" className="service-detail__heading">
          Formidlingstjeneste
        </Heading>
        <List className="service-detail__resource">
          <ResourceListItem
            id="laster-formidlingstjeneste"
            resourceName="Laster formidlingstjeneste"
            ownerName="Laster eier"
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
