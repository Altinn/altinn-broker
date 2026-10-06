import { useMemo, useState } from 'react'
import { Alert, Button, List } from '@altinn/altinn-components'
import { useFileTransferList, type FetchFileTransferPage } from '../../api/hooks/useFileTransferList'
import { FileTransferCard } from './FileTransferCard'
import { FileTransferFilters } from './FileTransferFilters'
import '../../pages/pages.css'
import type { SelectedParty } from '../../parties/PartiesContext'

type FileTransferListProps = {
  heading: string
  loadingText: string
  loadErrorText: string
  emptyStateText: string
  /** Keeps the two list views apart in the query cache. */
  queryKey: string
  currentOrg: SelectedParty
  fetchTransfers: FetchFileTransferPage
  toPath: (transferId: string) => string
}

export function FileTransferList({
  heading,
  loadingText,
  loadErrorText,
  emptyStateText,
  queryKey,
  currentOrg,
  fetchTransfers,
  toPath,
}: FileTransferListProps) {
  const [search, setSearch] = useState('')
  const [resourceFilter, setResourceFilter] = useState('')

  const { resources, items, isLoading, isError, hasNextPage, isFetchingNextPage, fetchNextPage } =
    useFileTransferList(queryKey, fetchTransfers, currentOrg)

  // Narrows what has been loaded so far. Reaching further back is what "Hent flere" is for.
  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase()
    return items.filter((overview) => {
      const reference = overview.sendersFileTransferReference || overview.fileTransferId
      const matchesSearch = !query || reference.toLowerCase().includes(query)
      const matchesResource = !resourceFilter || overview.resourceId === resourceFilter
      return matchesSearch && matchesResource
    })
  }, [items, search, resourceFilter])

  const resourceName = (resourceId: string) =>
    resources.find((resource) => resource.resourceId === resourceId)?.name ?? resourceId

  const isFiltered = Boolean(search.trim() || resourceFilter)

  return (
    <div className="page">
      <h2 className="page-heading">{heading}</h2>

      <FileTransferFilters
        search={search}
        onSearchChange={setSearch}
        resourceFilter={resourceFilter}
        onResourceFilterChange={setResourceFilter}
        resources={resources}
      />

      {isError && (
        <Alert variant="danger" heading="Kunne ikke hente formidlinger" message={loadErrorText} />
      )}

      {isLoading && <p className="empty-state">{loadingText}</p>}

      {!isLoading && !isError && filtered.length === 0 && (
        <p className="empty-state">
          {isFiltered && hasNextPage
            ? 'Ingen treff blant formidlingene som er lastet. Hent flere for å søke lenger tilbake.'
            : emptyStateText}
        </p>
      )}

      <List className="card-list">
        {filtered.map((overview) => (
          <li key={overview.fileTransferId}>
            <FileTransferCard
              resourceName={resourceName(overview.resourceId)}
              isSender={overview.isSender}
              sender={overview.sender}
              recipients={overview.recipients}
              currentActorName={currentOrg.name}
              reference={overview.sendersFileTransferReference || overview.fileTransferId}
              to={toPath(overview.fileTransferId)}
            />
          </li>
        ))}
      </List>

      {hasNextPage && (
        <div className="load-more-row">
          <Button
            variant="outline"
            size="lg"
            disabled={isFetchingNextPage}
            onClick={() => void fetchNextPage()}
          >
            {isFetchingNextPage ? 'Henter …' : 'Hent flere'}
          </Button>
        </div>
      )}
    </div>
  )
}
