import { useEffect, useMemo, useState } from 'react'
import type {
  DateRange,
  FileTransferSummaryPage,
} from '../../api/fileTransferSummary'
import { fetchAuthorizedResources, type AuthorizedResource } from '../../api/resources'
import { FileTransferCard } from './FileTransferCard'
import { FileTransferFilters } from './FileTransferFilters'
import { FileTransferPagination } from './FileTransferPagination'
import '../../pages/pages.css'
import { Alert, List } from '@altinn/altinn-components'
import type { SelectedParty } from '../../parties/PartiesContext'

const PAGE_SIZE = 5

const EMPTY_PAGE: FileTransferSummaryPage = { items: [], hasMore: false, pageSize: 0 }

type FileTransferListProps = {
  heading: string
  loadingText: string
  loadErrorText: string
  emptyStateText: string
  currentOrg: SelectedParty
  fetchTransfers: (
    resourceIds: string[],
    onBehalfOf: SelectedParty,
    range: DateRange,
  ) => Promise<FileTransferSummaryPage>
  toPath: (transferId: string) => string
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
  currentOrg,
  fetchTransfers,
  toPath,
}: FileTransferListProps) {
  const [resources, setResources] = useState<AuthorizedResource[] | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [search, setSearch] = useState('')
  const [resourceFilter, setResourceFilter] = useState('')
  const [from, setFrom] = useState<string | undefined>(undefined)
  const [to, setTo] = useState<string | undefined>(undefined)
  const [page, setPage] = useState(1)
  /** Keyed by the dates it was fetched for, so a page from a previous range counts as not loaded. */
  const [loaded, setLoaded] = useState<{ key: string; page: FileTransferSummaryPage } | null>(null)

  const range = useMemo<DateRange>(() => ({ from, to }), [from, to])
  const rangeKey = `${from ?? ''}|${to ?? ''}`

  useEffect(() => {
    let active = true

    fetchAuthorizedResources(currentOrg.organizationNumber)
      .then((authorized) => {
        if (active) {
          setResources(authorized)
        }
      })
      .catch(() => {
        if (active) {
          setLoadError(true)
        }
      })

    return () => {
      active = false
    }
  }, [currentOrg.organizationNumber])

  useEffect(() => {
    if (!resources) {
      return
    }

    let active = true
    const key = rangeKey
    const resourceIds = resources.map((resource) => resource.resourceId)
    // Nothing to ask about without a resource, and the API would reject an empty list anyway.
    const request =
      resourceIds.length === 0
        ? Promise.resolve(EMPTY_PAGE)
        : fetchTransfers(resourceIds, currentOrg, range)

    request
      .then((transfers) => {
        if (active) {
          setLoaded({ key, page: transfers })
        }
      })
      .catch(() => {
        if (active) {
          setLoadError(true)
        }
      })

    return () => {
      active = false
    }
  }, [resources, rangeKey, range, currentOrg, fetchTransfers])

  const current = loaded?.key === rangeKey ? loaded.page : null
  const isLoading = !loadError && current === null

  const filtered = useMemo(() => {
    if (!current) return []
    const query = search.trim().toLowerCase()
    return current.items.filter((overview) => {
      const reference = overview.sendersFileTransferReference || overview.fileTransferId
      const matchesSearch = !query || reference.toLowerCase().includes(query)
      const matchesResource = !resourceFilter || overview.resourceId === resourceFilter
      return matchesSearch && matchesResource
    })
  }, [current, search, resourceFilter])

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const cards = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE).map((overview) => ({
    fileTransferId: overview.fileTransferId,
    resourceName:
      resources?.find((r) => r.resourceId === overview.resourceId)?.name ?? overview.resourceId,
    isSender: overview.isSender,
    sender: overview.sender,
    recipients: overview.recipients,
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

  function handleRange(next: DateRange) {
    setFrom(next.from)
    setTo(next.to)
    setPage(1)
  }

  const isFiltered = Boolean(search.trim() || resourceFilter)

  return (
    <div className="page">
      <h2 className="page-heading">{heading}</h2>

      <FileTransferFilters
        search={search}
        onSearchChange={handleSearch}
        resourceFilter={resourceFilter}
        onResourceFilterChange={handleServiceFilter}
        resources={resources ?? []}
        range={range}
        onRangeChange={handleRange}
      />

      {loadError && <p className="empty-state">{loadErrorText}</p>}

      {isLoading && <p className="empty-state">{loadingText}</p>}

      {/* Without this the list passes for complete, and a search that comes up empty reads as
          "does not exist" rather than "not among the ones we fetched". */}
      {current?.hasMore && (
        <Alert
          variant="info"
          heading={`Viser de ${current.pageSize} nyeste`}
          message="Det finnes flere formidlinger enn dette. Velg en fra- og til-dato for å se eldre."
        />
      )}

      {current && cards.length === 0 && (
        <p className="empty-state">
          {current.hasMore && isFiltered
            ? `Ingen treff blant de ${current.pageSize} nyeste. Avgrens med dato for å søke lenger tilbake.`
            : emptyStateText}
        </p>
      )}

      <List className="card-list">
        {cards.map((card) => (
          <li key={card.fileTransferId}>
            <FileTransferCard
              resourceName={card.resourceName}
              isSender={card.isSender}
              sender={card.sender}
              recipients={card.recipients}
              currentActorName={currentOrg.name}
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
