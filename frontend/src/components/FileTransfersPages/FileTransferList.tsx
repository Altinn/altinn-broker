import { useEffect, useState } from 'react'
import {
  Alert,
  Button,
  List,
  Toolbar,
  ToolbarFilter,
  ToolbarSearch,
  type FilterState,
} from '@altinn/altinn-components'
import { useFileTransferList, type FetchFileTransferPage } from '../../api/hooks/useFileTransferList'
import { MIN_SEARCH_LENGTH } from '../../api/fileTransferSummary'
import { readFilterSelection, useFileTransferFilters } from './useFileTransferFilters'
import { FileTransferCard } from './FileTransferCard'
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
  const [filterState, setFilterState] = useState<FilterState>({})
  // The search goes to the API, so wait for a pause in typing rather than firing per keystroke.
  const debouncedSearch = useDebounced(search, 300)

  const { resourceFilter, role } = readFilterSelection(filterState)

  const {
    resources,
    items,
    appliedSearch,
    isLoading,
    isError,
    hasNextPage,
    isFetchingNextPage,
    fetchNextPage,
  } = useFileTransferList(queryKey, fetchTransfers, currentOrg, debouncedSearch, role, resourceFilter)

  // Needs the resources to name the services it offers.
  const { filters, getFilterLabel } = useFileTransferFilters(resources)

  const resourceName = (resourceId: string) =>
    resources.find((resource) => resource.resourceId === resourceId)?.name ?? resourceId

  const isFiltered = Boolean(appliedSearch || resourceFilter || role !== 'Both')

  return (
    <div className="page">
      <h2 className="page-heading">{heading}</h2>

      <Toolbar>
        <ToolbarSearch
          name="file-transfer-search"
          label="Søk på referanse"
          placeholder="Søk på referanse"
          value={search}
          minLength={MIN_SEARCH_LENGTH}
          onChange={(event) => setSearch((event.target as HTMLInputElement).value)}
          onClear={() => setSearch('')}
        />
        <ToolbarFilter
          filters={filters}
          filterState={filterState}
          onFilterStateChange={setFilterState}
          getFilterLabel={getFilterLabel}
          addLabel="Legg til filter"
          addNextLabel="Legg til"
          removeLabel="Fjern filter"
          resetLabel="Nullstill filtre"
          submitLabel="Vis treff"
        />
      </Toolbar>

      {isError && (
        <Alert variant="danger" heading="Kunne ikke hente formidlinger" message={loadErrorText} />
      )}

      {isLoading && <p className="empty-state">{loadingText}</p>}

      {!isLoading && !isError && items.length === 0 && (
        <p className="empty-state">
          {isFiltered ? 'Ingen formidlinger passer søket.' : emptyStateText}
        </p>
      )}

      <List className="card-list">
        {items.map((overview) => (
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
