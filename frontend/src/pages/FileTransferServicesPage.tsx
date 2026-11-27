import { Alert, Button, ButtonLabel, List, ResourceListItem } from '@altinn/altinn-components'
import { Select } from '@digdir/designsystemet-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
import { FileTransferServiceCard } from '../components/FileTransferServices/FileTransferServiceCard'
import { ServiceOwnerProvisioningBanner } from '../components/FileTransferServices/ServiceOwnerProvisioningBanner'
import { ServiceOwnerSetupNotice } from '../components/FileTransferServices/ServiceOwnerSetupNotice'
import { useServiceOwnerProvisioning } from '../components/FileTransferServices/useServiceOwnerProvisioning'
import { SearchField } from '../components/SearchField'
import { useParties } from '../parties/PartiesContext'
import './pages.css'

const serviceName = (service: AuthorizedResource) => service.name ?? service.resourceId

/** The access the list is narrowed to. Empty shows every service. */
type AccessFilter = '' | 'canSend' | 'canReceive' | 'isOwned'

/** The resources of one party. Anything for another party is stale after an actor switch. */
type ResourcesState =
  | { party: string; status: 'loaded'; services: AuthorizedResource[] }
  | { party: string; status: 'failed' }

export function FileTransferServicesPage() {
  const { t } = useTranslation()
  const { status: partiesStatus, selectedParty } = useParties()
  const [search, setSearch] = useState('')
  const [accessFilter, setAccessFilter] = useState<AccessFilter>('')
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
    return services.filter((service) => {
      const matchesSearch = serviceName(service).toLowerCase().includes(query)
      const matchesAccess = !accessFilter || service[accessFilter]
      return matchesSearch && matchesAccess
    })
  }, [services, search, accessFilter])

  const {
    provisioning,
    allowed,
    starting,
    error: provisioningError,
    accessNotice,
    start,
    retry,
  } = useServiceOwnerProvisioning(party, organizationName, reloadResources)
  const canProvision = allowed
  const provisioningPending = provisioning != null && provisioning.status !== 'Ready'
  // The notice and the banner say what is missing, so they also explain an empty list.
  const showOwnerSetup = accessNotice != null || provisioningPending
  const configurationLocked = provisioningError != null || provisioningPending
  // A service the party can send or receive on stays open while the service owner is being
  // provisioned. One the party only owns has nothing to offer until then.
  const isLocked = (service: AuthorizedResource) =>
    configurationLocked && service.isOwned && !service.canSend && !service.canReceive

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

        <div className="field">
          <label className="page-subheading" htmlFor="access-filter">
            {t('page.services.filter')}
          </label>
          <Select
            id="access-filter"
            aria-label={t('page.services.filter')}
            value={accessFilter}
            onChange={(e) => setAccessFilter(e.target.value as AccessFilter)}
          >
            <Select.Option value="">{t('page.services.filter_all')}</Select.Option>
            <Select.Option value="canSend">{t('page.services.can_send')}</Select.Option>
            <Select.Option value="canReceive">{t('page.services.can_receive')}</Select.Option>
            <Select.Option value="isOwned">{t('page.services.service_owner')}</Select.Option>
          </Select>
        </div>
      </div>

      {provisioningError && !provisioning && (
        <div className="page-section">
          <Alert variant="danger" heading={provisioningError} message={t('page.common.try_later')} />
          <Button size="sm" onClick={() => void retry()}>
            <ButtonLabel>{t('page.services.provision_retry')}</ButtonLabel>
          </Button>
        </div>
      )}

      {current?.status === 'failed' && (
        <Alert
          variant="danger"
          heading={t('page.services.load_failed_heading')}
          message={t('page.common.try_later')}
        />
      )}

      {current?.status === 'loaded' && services.length === 0 && !showOwnerSetup && (
        <Alert
          variant="info"
          heading={t('page.services.empty_heading')}
          message={t('page.services.empty_message', { name: organizationName })}
        />
      )}

      {provisioningPending && (
        <div className="page-section">
          <ServiceOwnerProvisioningBanner
            organizationName={organizationName}
            provisioning={provisioning}
            canStart={canProvision}
            starting={starting}
            error={provisioningError}
            onStart={start}
          />
        </div>
      )}

      <div className="page-section">
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

        {services.length > 0 && filtered.length === 0 && (
          <p className="empty-state">{t('page.services.no_matches')}</p>
        )}

        <List className="card-list">
          {filtered.map((service) => (
            <li key={service.resourceId}>
              <FileTransferServiceCard service={service} locked={isLocked(service)} />
            </li>
          ))}
        </List>
      </div>

      {accessNotice && (
        <div className="page-section">
          <ServiceOwnerSetupNotice />
        </div>
      )}
    </div>
  )
}
