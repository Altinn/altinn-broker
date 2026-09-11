import { Pagination, Search, Select } from '@digdir/designsystemet-react'
import { useEffect, useMemo, useState } from 'react'
import { getActiveFileTransfers, type ActiveFileTransfers } from '../api/activeFileTransfers'
import { fetchAuthorizedResources, type AuthorizedResource } from '../api/resources'
import { ActiveFileTransferCard } from '../components/ActiveFileTransferCard'
import { activeTransferPath } from './routes'
import './pages.css'

const PAGE_SIZE = 5

const current_org = "312936496"

export function ActiveFileTransfersPage() {
  const [resources, setResources] = useState<AuthorizedResource[]>([])
  const [overviews, setOverviews] = useState<ActiveFileTransfers[] | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [search, setSearch] = useState('')
  const [resourceFilter, setResourceFilter] = useState('')
  const [page, setPage] = useState(1)

  useEffect(() => {
    async function loadActiveFileTransfers() {
      try {
        const authorizedResources = await fetchAuthorizedResources(current_org)
        setResources(authorizedResources)

        const resourceIds = authorizedResources.map((resource) => resource.resourceId)
        if (resourceIds.length === 0) {
          setOverviews([])
          return
        }
        setOverviews(await getActiveFileTransfers(resourceIds, current_org))
      } catch {
        setLoadError(true)
      }
    }
    void loadActiveFileTransfers()
  }, [])

  const filtered = useMemo(() => {
    if (!overviews) return []
    const query = search.trim().toLowerCase()
    return overviews.filter((overview) => {
      const reference = overview.sendersFileTransferReference || overview.fileTransferId
      const matchesSearch = !query || reference.toLowerCase().includes(query)
      const matchesResource = !resourceFilter || overview.resourceId === resourceFilter
      return matchesSearch && matchesResource
    })
  }, [overviews, resources, search, resourceFilter])

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const cards = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE).map((overview) => ({
    fileTransferId: overview.fileTransferId,
    resourceName: resources.find((r) => r.resourceId === overview.resourceId)?.name ?? overview.resourceId,
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

      {loadError && <p className="empty-state">Klarte ikke å hente aktive formidlinger.</p>}

      {!loadError && overviews === null && <p className="empty-state">Laster aktive formidlinger …</p>}

      {!loadError && overviews !== null && cards.length === 0 && (
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
        <Pagination aria-label="Sidenavigering" data-current={String(page)} data-total={String(totalPages)}>
          <Pagination.List>
            <Pagination.Item>
              <Pagination.Button
                aria-label="Forrige side"
                disabled={page === 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Forrige
              </Pagination.Button>
            </Pagination.Item>
            {Array.from({ length: totalPages }, (_, i) => i + 1).map((n) => (
              <Pagination.Item key={n}>
                <Pagination.Button
                  aria-label={`Side ${n}`}
                  aria-current={n === page ? 'page' : undefined}
                  onClick={() => setPage(n)}
                >
                  {n}
                </Pagination.Button>
              </Pagination.Item>
            ))}
            <Pagination.Item>
              <Pagination.Button
                aria-label="Neste side"
                disabled={page === totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
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
