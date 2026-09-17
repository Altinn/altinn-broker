import { useEffect, useMemo, useState } from 'react'
import type { FileTransferSummary } from '../../api/fileTransferSummary'
import { fetchAuthorizedResources, type AuthorizedResource } from '../../api/resources'
import { FileTransferCard } from './FileTransferCard'
import { FileTransferFilters } from './FileTransferFilters'
import { FileTransferPagination } from './FileTransferPagination'
import '../../pages/pages.css'
import { List } from '@altinn/altinn-components'

const PAGE_SIZE = 5

type FileTransferListProps = {
  heading: string
  loadingText: string
  loadErrorText: string
  emptyStateText: string
  currentOrg: string
  fetchTransfers: (resourceIds: string[], onBehalfOf: string) => Promise<FileTransferSummary[]>
  toPath: (transferId: string) => string
}

export function FileTransferList({
  heading,
  loadingText,
  loadErrorText,
  emptyStateText,
  currentOrg,
  fetchTransfers,
  toPath,
}: FileTransferListProps) {
  const [resources, setResources] = useState<AuthorizedResource[]>([])
  const [overviews, setOverviews] = useState<FileTransferSummary[] | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [search, setSearch] = useState('')
  const [resourceFilter, setResourceFilter] = useState('')
  const [page, setPage] = useState(1)

  useEffect(() => {
    async function loadFileTransfers() {
      try {
        const authorizedResources = await fetchAuthorizedResources(currentOrg)
        setResources(authorizedResources)

        const resourceIds = authorizedResources.map((resource) => resource.resourceId)
        if (resourceIds.length === 0) {
          setOverviews([])
          return
        }
        setOverviews(await fetchTransfers(resourceIds, currentOrg))
      } catch {
        setLoadError(true)
      }
    }
    void loadFileTransfers()
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
      <h2 className="page-heading">{heading}</h2>

      <FileTransferFilters
        search={search}
        onSearchChange={handleSearch}
        resourceFilter={resourceFilter}
        onResourceFilterChange={handleServiceFilter}
        resources={resources}
      />

      {loadError && <p className="empty-state">{loadErrorText}</p>}

      {!loadError && overviews === null && <p className="empty-state">{loadingText}</p>}

      {!loadError && overviews !== null && cards.length === 0 && (
        <p className="empty-state">{emptyStateText}</p>
      )}

      <List className="card-list">
        {cards.map((card) => (
          <li key={card.fileTransferId}>
            <FileTransferCard
              resourceName={card.resourceName}
              sender={card.sender}
              recipient={card.recipient}
              reference={card.reference}
              to={toPath(card.fileTransferId)}
            />
          </li>
        ))}
      </List>

      <FileTransferPagination page={page} totalPages={totalPages} onPageChange={setPage} />
    </div>
  )
}
