import { Alert, Heading, List, ResourceListItem, Searchbar } from '@altinn/altinn-components'
import { useEffect, useMemo, useState } from 'react'
import { Link, type LinkProps } from 'react-router-dom'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
import { OrganizationHeader } from '../components/OrganizationHeader'
import { useParties } from '../parties/PartiesContext'
import { servicePath } from './routes'
import './pages.css'

const serviceName = (service: AuthorizedResource) => service.name ?? service.resourceId

/** The resources of one party. Anything for another party is stale after an actor switch. */
type ResourcesState =
  | { party: string; status: 'loaded'; services: AuthorizedResource[] }
  | { party: string; status: 'failed' }

export function FileTransferServicesPage() {
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

  const creatable = filtered.filter((service) => service.canSend)
  const other = filtered.filter((service) => !service.canSend)

  const serviceItem = (service: AuthorizedResource, variant: 'default' | 'subtle') => (
    <ResourceListItem
      key={service.resourceId}
      id={service.resourceId}
      resourceName={serviceName(service)}
      ownerName={service.serviceOwnerName ?? 'Ukjent eier'}
      description={service.serviceOwnerName ? `Eid av ${service.serviceOwnerName}` : undefined}
      variant={variant}
      interactive
      as={(props: LinkProps) => <Link {...props} to={servicePath(service.resourceId)} />}
    />
  )

  if (partiesStatus === 'failed') {
    return (
      <div className="page">
        <Alert
          variant="danger"
          heading="Kunne ikke hente aktører"
          message="Vi fikk ikke hentet hvilke virksomheter du kan representere. Prøv igjen senere."
        />
      </div>
    )
  }

  if (partiesStatus === 'loaded' && !selectedParty) {
    return (
      <div className="page">
        <Alert
          variant="info"
          heading="Ingen virksomheter"
          message="Du kan ikke representere noen virksomheter i BrokerBox."
        />
      </div>
    )
  }

  return (
    <div className="page">
      <OrganizationHeader />

      <Searchbar
        name="service-search"
        value={search}
        placeholder="Søk etter formidlingstjenester"
        onChange={(event) => setSearch((event.target as HTMLInputElement).value)}
        onClear={() => setSearch('')}
      />

      {isLoading && (
        <List spacing="sm">
          {[1, 2, 3].map((placeholder) => (
            <ResourceListItem
              key={placeholder}
              id={`placeholder-${placeholder}`}
              resourceName="Laster formidlingstjeneste"
              ownerName="Laster eier"
              loading
            />
          ))}
        </List>
      )}

      {current?.status === 'failed' && (
        <Alert
          variant="danger"
          heading="Kunne ikke hente formidlingstjenester"
          message="Prøv igjen senere."
        />
      )}

      {current?.status === 'loaded' && services.length === 0 && (
        <Alert
          variant="info"
          heading="Ingen formidlingstjenester"
          message={`${organizationName} har ikke tilgang til noen formidlingstjenester.`}
        />
      )}

      {creatable.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            Formidlingstjenester {organizationName} kan opprette
          </Heading>
          <List spacing="sm">{creatable.map((service) => serviceItem(service, 'default'))}</List>
        </section>
      )}

      {other.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            Andre formidlingstjenester {organizationName} er delaktig i
          </Heading>
          <List spacing="sm">{other.map((service) => serviceItem(service, 'subtle'))}</List>
        </section>
      )}
    </div>
  )
}
