import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import {
  MIN_SEARCH_LENGTH,
  type FileTransferSummary,
  type FileTransferSummaryPage,
} from '../fileTransferSummary'
import { fetchAuthorizedResources } from '../resources'
import type { SelectedParty } from '../../parties/PartiesContext'

export type FetchFileTransferPage = (
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
  search: string | undefined,
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
) {
  const resources = useQuery({
    queryKey: ['authorized-resources', party.organizationNumber],
    queryFn: () => fetchAuthorizedResources(party.organizationNumber),
  })

  // Below the minimum the API ignores the term, so keep it out of the key and query unfiltered
  // rather than refetching for every keystroke on the way to three characters.
  const appliedSearch = search.trim().length >= MIN_SEARCH_LENGTH ? search.trim() : ''

  const transfers = useInfiniteQuery({
    queryKey: [queryKey, party.organizationNumber, appliedSearch],
    enabled: resources.data !== undefined,
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      fetchPage(
        (resources.data ?? []).map((resource) => resource.resourceId),
        party,
        pageParam,
        appliedSearch || undefined,
      ),
    getNextPageParam: (lastPage) => lastPage.continuationToken ?? undefined,
  })

  const items: FileTransferSummary[] = transfers.data?.pages.flatMap((page) => page.items) ?? []

  return {
    resources: resources.data ?? [],
    items,
    appliedSearch,
    isLoading: resources.isLoading || transfers.isPending,
    isError: resources.isError || transfers.isError,
    hasNextPage: transfers.hasNextPage,
    isFetchingNextPage: transfers.isFetchingNextPage,
    fetchNextPage: transfers.fetchNextPage,
  }
}
