import { Alert, Heading, List, ResourceListItem } from '@altinn/altinn-components'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, type LinkProps } from 'react-router-dom'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
import { ServiceOwnerProvisioningBanner } from '../components/FileTransferServices/ServiceOwnerProvisioningBanner'
import { ServiceOwnerSetupNotice } from '../components/FileTransferServices/ServiceOwnerSetupNotice'
import { useServiceOwnerProvisioning } from '../components/FileTransferServices/useServiceOwnerProvisioning'
import { SearchField } from '../components/SearchField'
import { useParties } from '../parties/PartiesContext'
import { servicePath } from './routes'
import './pages.css'

const serviceName = (service: AuthorizedResource) => service.name ?? service.resourceId

/** The resources of one party. Anything for another party is stale after an actor switch. */
type ResourcesState =
  | { party: string; status: 'loaded'; services: AuthorizedResource[] }
  | { party: string; status: 'failed' }

export function FileTransferServicesPage() {
  const { t } = useTranslation()
  const { status: partiesStatus, selectedParty } = useParties()
  const [search, setSearch] = useState('')
  const [resources, setResources] = useState<ResourcesState | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const reloadResources = useCallback(() => setReloadToken((token) => token + 1), [])

  const party = selectedParty?.organizationNumber
  const organizationName = selectedParty?.name ?? ''

  useEffect(() => {
    if (!party) {
      return
    }

    let active = true
    fetchAuthorizedResources(party)
      .then((authorized) => {
        if (active) {
          setResources({ party, status: 'loaded', services: authorized })
        }
      })
      .catch(() => {
        if (active) {
          setResources({ party, status: 'failed' })
        }
      })

    return () => {
      active = false
    }
  }, [party, reloadToken])

  const current = resources?.party === party ? resources : null
  const services = useMemo(
    () => (current?.status === 'loaded' ? current.services : []),
    [current],
  )
  const isLoading = partiesStatus === 'loading' || (Boolean(party) && current === null)

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase()
    return services.filter((service) => serviceName(service).toLowerCase().includes(query))
  }, [services, search])

  const sendable = filtered.filter((service) => service.canSend)
  const receivable = filtered.filter((service) => service.canReceive)
  const receivableNotSendable = receivable.filter((service) => !service.canSend)
  const configurable = filtered.filter((service) => service.isOwned)
  const owned = services.filter((service) => service.isOwned)
  const isServiceOwner = services.some((service) => service.isServiceOwner)
  const hasConfigureAccess = services.some((service) => service.hasConfigureAccess)
  const {
    provisioning,
    allowed,
    starting,
    error: provisioningError,
    accessNotice,
    start,
  } = useServiceOwnerProvisioning(party, organizationName, reloadResources)
  const canProvision = allowed
  const showConfigure =
    accessNotice != null ||
    (owned.length > 0 && (isServiceOwner || hasConfigureAccess || allowed)) ||
    (isServiceOwner && hasConfigureAccess && provisioning != null && provisioning.status !== 'Ready') ||
    (allowed && provisioning != null && provisioning.status !== 'Ready')
  const configurationLocked = showConfigure && provisioning != null && provisioning.status !== 'Ready'

  const serviceItem = (service: AuthorizedResource) => (
    <ResourceListItem
      key={service.resourceId}
      id={service.resourceId}
      resourceName={serviceName(service)}
      ownerName={service.serviceOwnerName ?? t('page.common.unknown_owner')}
      description={service.serviceOwnerName ? t('page.common.owned_by', { name: service.serviceOwnerName }) : undefined}
      variant="default"
      interactive
      as={(props: LinkProps) => <Link {...props} to={servicePath(service.resourceId)} />}
    />
  )

  const lockedServiceItem = (service: AuthorizedResource) => (
    <ResourceListItem
      key={service.resourceId}
      id={service.resourceId}
      resourceName={serviceName(service)}
      ownerName={service.serviceOwnerName ?? t('page.common.unknown_owner')}
      description={t('page.services.provision_pending_resource')}
      variant="subtle"
      interactive={false}
    />
  )

  if (partiesStatus === 'failed') {
    return (
      <div className="page">
        <Alert
          variant="danger"
          heading={t('page.parties.load_failed_heading')}
          message={t('page.parties.load_failed_message')}
        />
      </div>
    )
  }

  if (partiesStatus === 'loaded' && !selectedParty) {
    return (
      <div className="page">
        <Alert
          variant="info"
          heading={t('page.parties.none_heading')}
          message={t('page.parties.none_message')}
        />
      </div>
    )
  }

  return (
    <div className="page">
      <h1 className="page-heading">{t('page.services.title')}</h1>

      <div className="filter-row">
        <SearchField
          id="service-search"
          label={t('page.services.search')}
          value={search}
          onChange={setSearch}
        />
      </div>

      {isLoading && (
        <List spacing="sm">
          {[1, 2, 3].map((placeholder) => (
            <ResourceListItem
              key={placeholder}
              id={`placeholder-${placeholder}`}
              resourceName={t('page.services.loading_name')}
              ownerName={t('page.services.loading_owner')}
              loading
            />
          ))}
        </List>
      )}

      {current?.status === 'failed' && (
        <Alert
          variant="danger"
          heading={t('page.services.load_failed_heading')}
          message={t('page.common.try_later')}
        />
      )}

      {current?.status === 'loaded' && services.length === 0 && !showConfigure && (
        <Alert
          variant="info"
          heading={t('page.services.empty_heading')}
          message={t('page.services.empty_message', { name: organizationName })}
        />
      )}

      {sendable.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            {t('page.services.can_send', { name: organizationName })}
          </Heading>
          <List spacing="sm">{sendable.map(serviceItem)}</List>
        </section>
      )}

      {receivableNotSendable.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            {t('page.services.involved', { name: organizationName })}
          </Heading>
          <List spacing="sm">{receivableNotSendable.map(serviceItem)}</List>
        </section>
      )}

      {showConfigure && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            {t('page.services.can_configure', { name: organizationName })}
          </Heading>
          {accessNotice && <ServiceOwnerSetupNotice />}
          {provisioning && (
            <ServiceOwnerProvisioningBanner
              organizationName={organizationName}
              provisioning={provisioning}
              canStart={canProvision}
              starting={starting}
              error={provisioningError}
              onStart={start}
            />
          )}
          {configurable.length > 0 && (
            <List className={configurationLocked ? 'configure-pending' : undefined} spacing="sm">
              {configurable.map((service) => (configurationLocked ? lockedServiceItem(service) : serviceItem(service)))}
            </List>
          )}
        </section>
      )}
    </div>
  )
}
