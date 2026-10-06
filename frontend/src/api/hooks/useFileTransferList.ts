import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query'
import {
  MIN_SEARCH_LENGTH,
  type FileTransferRole,
  type FileTransferSummary,
  type FileTransferSummaryPage,
} from '../fileTransferSummary'
import { fetchAuthorizedResources } from '../resources'
import type { SelectedParty } from '../../parties/PartiesContext'

/** How long a result stays fresh. Matches what Arbeidsflate uses for its dialog list. */
const CACHE_TIME_MS = 1000 * 60 * 10

export type FetchFileTransferPage = (
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
  search: string | undefined,
  role: FileTransferRole,
) => Promise<FileTransferSummaryPage>

/**
 * The file transfers of one list view, a page at a time.
 *
 * The resources are a query of their own: the lookup is an authorization call per broker resource,
 * so it is cached and shared rather than repeated for every page and every list.
 */
export function useFileTransferList(
  queryKey: string,
  fetchPage: FetchFileTransferPage,
  party: SelectedParty,
  search: string,
  role: FileTransferRole,
  /** A single resource to limit the query to, or empty for all the party has access to. */
  resourceFilter: string,
) {
  const resources = useQuery({
    queryKey: ['authorized-resources', party.organizationNumber],
    queryFn: () => fetchAuthorizedResources(party.organizationNumber),
    staleTime: CACHE_TIME_MS,
  })

  // Below the minimum the API ignores the term, so keep it out of the key and query unfiltered
  // rather than refetching for every keystroke on the way to three characters.
  const appliedSearch = search.trim().length >= MIN_SEARCH_LENGTH ? search.trim() : ''

  // Narrowing here rather than after the fetch: filtering a loaded page would show three rows of
  // a hundred and leave "Hent flere" chasing the rest.
  const resourceIds = (resources.data ?? [])
    .map((resource) => resource.resourceId)
    .filter((resourceId) => !resourceFilter || resourceId === resourceFilter)

  const transfers = useInfiniteQuery({
    queryKey: [queryKey, party.organizationNumber, appliedSearch, role, resourceFilter],
    enabled: resources.data !== undefined,
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      fetchPage(
        resourceIds,
        party,
        pageParam,
        appliedSearch || undefined,
        role,
      ),
    getNextPageParam: (lastPage) => lastPage.continuationToken ?? undefined,
    staleTime: CACHE_TIME_MS,
    // Keeps the previous rows on screen while a new filter loads, instead of blanking the list
    // every time the query key changes.
    placeholderData: keepPreviousData,
  })

  const items: FileTransferSummary[] = transfers.data?.pages.flatMap((page) => page.items) ?? []

  return {
    resources: resources.data ?? [],
    items,
    appliedSearch,
    // isFetching with placeholder data means this key has never been fetched - the rows on screen
    // belong to the previous filter. Anything else is a background refresh, not a load.
    isLoading:
      resources.isLoading || transfers.isPending || (transfers.isFetching && transfers.isPlaceholderData),
    isError: resources.isError || transfers.isError,
    hasNextPage: transfers.hasNextPage,
    isFetchingNextPage: transfers.isFetchingNextPage,
    fetchNextPage: transfers.fetchNextPage,
  }
}
