import { Alert, Heading, List, ResourceListItem } from '@altinn/altinn-components'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, type LinkProps } from 'react-router-dom'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
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
  }, [party])

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
  const isServiceOwner = services.some((service) => service.isServiceOwner)

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

      {current?.status === 'loaded' && services.length === 0 && (
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

      {isServiceOwner && configurable.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            {t('page.services.can_configure', { name: organizationName })}
          </Heading>
          <List spacing="sm">{configurable.map(serviceItem)}</List>
        </section>
      )}
    </div>
  )
}
