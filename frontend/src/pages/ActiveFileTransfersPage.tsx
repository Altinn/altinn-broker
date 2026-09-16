import { Pagination, Search, Select } from '@digdir/designsystemet-react'
import { useEffect, useMemo, useState } from 'react'
import { getActiveFileTransfers, type ActiveFileTransfers } from '../api/activeFileTransfers'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
import { ActiveFileTransferCard } from '../components/ActiveFileTransferCard'
import { useParties } from '../parties/PartiesContext'
import { activeTransferPath } from './routes'
import './pages.css'

const PAGE_SIZE = 5

/** What was loaded for one party. Anything for another party is stale after an actor switch. */
type LoadedTransfers = {
  party: string
  resources: AuthorizedResource[]
  overviews: ActiveFileTransfers[]
}

export function ActiveFileTransfersPage() {
  const { status: partiesStatus, selectedParty } = useParties()
  const [loaded, setLoaded] = useState<LoadedTransfers | null>(null)
  const [failedParty, setFailedParty] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [resourceFilter, setResourceFilter] = useState('')
  const [page, setPage] = useState(1)

  const party = selectedParty?.organizationNumber

  useEffect(() => {
    if (!party) {
      return
    }

    let active = true
    async function loadActiveFileTransfers(partyId: string) {
      try {
        const authorizedResources = await fetchAuthorizedResources(partyId)
        const resourceIds = authorizedResources.map((resource) => resource.resourceId)
        const overviews =
          resourceIds.length === 0 ? [] : await getActiveFileTransfers(resourceIds, partyId)

        if (active) {
          setLoaded({ party: partyId, resources: authorizedResources, overviews })
        }
      } catch {
        if (active) {
          setFailedParty(partyId)
        }
      }
    }
    void loadActiveFileTransfers(party)

    return () => {
      active = false
    }
  }, [party])

  const current = loaded?.party === party ? loaded : null
  const resources = useMemo(() => current?.resources ?? [], [current])
  const overviews = current?.overviews ?? null
  const loadError = failedParty === party

  const filtered = useMemo(() => {
    if (!overviews) return []
    const query = search.trim().toLowerCase()
    return overviews.filter((overview) => {
      const reference = overview.sendersFileTransferReference || overview.fileTransferId
      const matchesSearch = !query || reference.toLowerCase().includes(query)
      const matchesResource = !resourceFilter || overview.resourceId === resourceFilter
      return matchesSearch && matchesResource
    })
  }, [overviews, search, resourceFilter])

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  // An actor switch can leave fewer results than the page the user was on.
  const currentPage = Math.min(page, totalPages)
  const cards = filtered
    .slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE)
    .map((overview) => ({
      fileTransferId: overview.fileTransferId,
      resourceName:
        resources.find((r) => r.resourceId === overview.resourceId)?.name ?? overview.resourceId,
      sender: overview.sender,
      recipient: overview.recipients.join(', '),
      reference: overview.sendersFileTransferReference || overview.fileTransferId,
    }))

  function handleSearch(value: string) {
    setSearch(value)
    setPage(1)
  }

  function handleServiceFilter(value: string) {
    setResourceFilter(value)
    setPage(1)
  }

  return (
    <div className="page">
      <h2 className="page-heading">Aktive formidlinger</h2>

      <div className="filter-row">
        <div className="field">
        <label className="page-subheading" htmlFor="search">
          Søk på referanse
        </label>
        <Search>
          <Search.Input
            aria-label="Søk på referanse"
            placeholder="Søk..."
            value={search}
            onChange={(e) => handleSearch(e.target.value)}
          />
          <Search.Clear onClick={() => handleSearch('')} />
        </Search>
        </div>

        <div className="field">
          <label className="page-subheading" htmlFor="resource-filter">
            Filtrer på tjeneste
          </label>
          <Select
            id="resource-filter"
            aria-label="Filtrer på tjeneste"
            value={resourceFilter}
            onChange={(e) => handleServiceFilter(e.target.value)}
          >
            <Select.Option value="">Alle tjenester</Select.Option>
            {resources.map((resource) => (
              <Select.Option key={resource.resourceId} value={resource.resourceId}>
                {resource.name ?? resource.resourceId}
              </Select.Option>
            ))}
          </Select>
        </div>
      </div>

      {partiesStatus === 'failed' && <p className="empty-state">Klarte ikke å hente aktører.</p>}

      {partiesStatus === 'loaded' && !party && (
        <p className="empty-state">Du kan ikke representere noen virksomheter i BrokerBox.</p>
      )}

      {party && loadError && <p className="empty-state">Klarte ikke å hente aktive formidlinger.</p>}

      {partiesStatus !== 'failed' && !loadError && (!party || overviews === null) && (
        <p className="empty-state">Laster aktive formidlinger …</p>
      )}

      {party && !loadError && overviews !== null && cards.length === 0 && (
        <p className="empty-state">Ingen aktive formidlinger funnet.</p>
      )}

      <ul className="card-list">
        {cards.map((card) => (
          <li key={card.fileTransferId}>
            <ActiveFileTransferCard
              resourceName={card.resourceName}
              sender={card.sender}
              recipient={card.recipient}
              reference={card.reference}
              to={activeTransferPath(card.fileTransferId)}
            />
          </li>
        ))}
      </ul>

      <div className="pagination-row">
      {totalPages > 1 && (
        <Pagination aria-label="Sidenavigering" data-current={String(currentPage)} data-total={String(totalPages)}>
          <Pagination.List>
            <Pagination.Item>
              <Pagination.Button
                aria-label="Forrige side"
                disabled={currentPage === 1}
                onClick={() => setPage(Math.max(1, currentPage - 1))}
              >
                Forrige
              </Pagination.Button>
            </Pagination.Item>
            {Array.from({ length: totalPages }, (_, i) => i + 1).map((n) => (
              <Pagination.Item key={n}>
                <Pagination.Button
                  aria-label={`Side ${n}`}
                  aria-current={n === currentPage ? 'page' : undefined}
                  onClick={() => setPage(n)}
                >
                  {n}
                </Pagination.Button>
              </Pagination.Item>
            ))}
            <Pagination.Item>
              <Pagination.Button
                aria-label="Neste side"
                disabled={currentPage === totalPages}
                onClick={() => setPage(Math.min(totalPages, currentPage + 1))}
              >
                Neste
              </Pagination.Button>
            </Pagination.Item>
          </Pagination.List>
        </Pagination>
      )}
      </div>
    </div>
  )
}
