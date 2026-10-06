import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import type { FileTransferSummary, FileTransferSummaryPage } from '../fileTransferSummary'
import { fetchAuthorizedResources } from '../resources'
import type { SelectedParty } from '../../parties/PartiesContext'

/** The authorization lookup behind it is expensive, so it is not repeated per navigation. */
const RESOURCES_STALE_TIME_MS = 1000 * 60 * 10

export type FetchFileTransferPage = (
  resourceIds: string[],
  onBehalfOf: SelectedParty | undefined,
  continuationToken: string | undefined,
) => Promise<FileTransferSummaryPage>

/**
 * The file transfers of one list view, a page at a time.
 *
 * The resources are a query of their own: the lookup is an authorization call per broker resource,
 * so it is cached and shared between the two list views rather than refetched for every page.
 */
export function useFileTransferList(
  queryKey: string,
  fetchPage: FetchFileTransferPage,
  party: SelectedParty,
) {
  const resources = useQuery({
    queryKey: ['authorized-resources', party.organizationNumber],
    queryFn: () => fetchAuthorizedResources(party.organizationNumber),
    staleTime: RESOURCES_STALE_TIME_MS,
  })

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

  const items: FileTransferSummary[] = transfers.data?.pages.flatMap((page) => page.items) ?? []

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
