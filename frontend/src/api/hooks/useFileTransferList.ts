import { useInfiniteQuery } from '@tanstack/react-query'
import type { FileTransferSummary, FileTransferSummaryPage } from '../fileTransferSummary'
import { fetchAuthorizedResources, type AuthorizedResource } from '../resources'
import type { SelectedParty } from '../../parties/PartiesContext'

export type FetchFileTransferPage = (
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
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
) {
  const resources = useQuery_AuthorizedResources(party)

  const transfers = useInfiniteQuery({
    queryKey: [queryKey, party.organizationNumber],
    enabled: resources.data !== undefined,
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      fetchPage(
        (resources.data ?? []).map((resource) => resource.resourceId),
        party,
        pageParam,
      ),
    getNextPageParam: (lastPage) => lastPage.continuationToken ?? undefined,
  })

  const items: FileTransferSummary[] =
    transfers.data?.pages.flatMap((page) => page.items) ?? []

  return {
    resources: resources.data ?? [],
    items,
    isLoading: resources.isLoading || transfers.isPending,
    isError: resources.isError || transfers.isError,
    hasNextPage: transfers.hasNextPage,
    isFetchingNextPage: transfers.isFetchingNextPage,
    fetchNextPage: transfers.fetchNextPage,
  }
}

/** The resources the party may act on. Its own query so the authorization call is not repeated. */
function useQuery_AuthorizedResources(party: SelectedParty) {
  return useInfiniteQuery({
    queryKey: ['authorized-resources', party.organizationNumber],
    initialPageParam: undefined,
    queryFn: () => fetchAuthorizedResources(party.organizationNumber),
    getNextPageParam: () => undefined,
    select: (data): AuthorizedResource[] => data.pages[0] ?? [],
  })
}
