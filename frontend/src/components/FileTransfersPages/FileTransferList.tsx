import { useMemo, useState } from 'react'
import { Button, List } from '@altinn/altinn-components'
import { useFileTransferList, type FetchFileTransferPage } from '../../api/hooks/useFileTransferList'
import { FileTransferCard } from './FileTransferCard'
import { FileTransferFilters } from './FileTransferFilters'
import { getDeadlineProps } from '../../helpers/deadlineHelper'
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
  /** A deadline only matters while the transfer can still be downloaded. */
  showDeadline?: boolean
}

/**
 * Mounted with the selected party as its key, so switching actor gives a fresh list rather than
 * one that has to unpick which request belonged to whom.
 */
export function FileTransferList({
  heading,
  loadingText,
  loadErrorText,
  emptyStateText,
  queryKey,
  currentOrg,
  fetchTransfers,
  toPath,
  showDeadline = false,
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

      {isError && <p className="empty-state">{loadErrorText}</p>}

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
              deadline={showDeadline ? getDeadlineProps(overview.expirationTime, overview.isSender) : undefined}
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
