import { useEffect, useMemo, useState } from 'react'
import { Alert, Button, List } from '@altinn/altinn-components'
import { useFileTransferList, type FetchFileTransferPage } from '../../api/hooks/useFileTransferList'
import { MIN_SEARCH_LENGTH } from '../../api/fileTransferSummary'
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
  // The search goes to the API, so wait for a pause in typing rather than firing per keystroke.
  const debouncedSearch = useDebounced(search, 300)

  const {
    resources,
    items,
    appliedSearch,
    isLoading,
    isError,
    hasNextPage,
    isFetchingNextPage,
    fetchNextPage,
  } = useFileTransferList(queryKey, fetchTransfers, currentOrg, debouncedSearch)

  // The reference search is the API's job now; only the service filter is narrowed here.
  const filtered = useMemo(
    () => items.filter((overview) => !resourceFilter || overview.resourceId === resourceFilter),
    [items, resourceFilter],
  )

  const resourceName = (resourceId: string) =>
    resources.find((resource) => resource.resourceId === resourceId)?.name ?? resourceId

  const isFiltered = Boolean(appliedSearch || resourceFilter)
  // Typed something, but not enough for the API to act on it.
  const searchTooShort = search.trim().length > 0 && search.trim().length < MIN_SEARCH_LENGTH

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

      {searchTooShort && (
        <p className="empty-state">Skriv minst {MIN_SEARCH_LENGTH} tegn for å søke.</p>
      )}

      {!isLoading && !isError && !searchTooShort && filtered.length === 0 && (
        <p className="empty-state">
          {isFiltered ? 'Ingen formidlinger passer søket.' : emptyStateText}
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

/** Holds back a value until the user stops changing it. */
function useDebounced<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}
