import { Alert, Heading, List, ResourceListItem, Searchbar } from '@altinn/altinn-components'
import { useEffect, useMemo, useState } from 'react'
import { Link, type LinkProps } from 'react-router-dom'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
import { OrganizationHeader } from '../components/OrganizationHeader'
import { currentOrganization } from '../data/mockData'
import { servicePath } from './routes'
import './pages.css'

// TODO: hardcoded for local verification against TT02. Use the party the user selects
// once Broker exposes the end user's authorized parties.
const currentParty = '313193748'

const serviceName = (service: AuthorizedResource) => service.name ?? service.resourceId

export function FileTransferServicesPage() {
  const [search, setSearch] = useState('')
  const [services, setServices] = useState<AuthorizedResource[]>([])
  const [status, setStatus] = useState<'loading' | 'loaded' | 'failed'>('loading')

  useEffect(() => {
    let active = true
    fetchAuthorizedResources(currentParty)
      .then((authorized) => {
        if (!active) {
          return
        }
        setServices(authorized)
        setStatus('loaded')
      })
      .catch(() => {
        if (active) {
          setStatus('failed')
        }
      })

    return () => {
      active = false
    }
  }, [])

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

      {status === 'loading' && (
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

      {status === 'failed' && (
        <Alert
          variant="danger"
          heading="Kunne ikke hente formidlingstjenester"
          message="Prøv igjen senere."
        />
      )}

      {status === 'loaded' && services.length === 0 && (
        <Alert
          variant="info"
          heading="Ingen formidlingstjenester"
          message={`${currentOrganization.name} har ikke tilgang til noen formidlingstjenester.`}
        />
      )}

      {creatable.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            Formidlingstjenester {currentOrganization.name} kan opprette
          </Heading>
          <List spacing="sm">{creatable.map((service) => serviceItem(service, 'default'))}</List>
        </section>
      )}

      {other.length > 0 && (
        <section className="page-section">
          <Heading size="sm" as="h2">
            Andre formidlingstjenester {currentOrganization.name} er delaktig i
          </Heading>
          <List spacing="sm">{other.map((service) => serviceItem(service, 'subtle'))}</List>
        </section>
      )}
    </div>
  )
}
