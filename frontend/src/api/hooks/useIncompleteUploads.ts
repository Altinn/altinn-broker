import { useQueries } from '@tanstack/react-query'
import { getDraftFileTransfers } from '../draftFileTransfers'
import type { FileTransferSummary } from '../fileTransferSummary'
import { getUploadPlan, type StoredUploadPlan } from '../uploadPlan'
import type { SelectedParty } from '../../parties/PartiesContext'
import { useFileTransferList } from './useFileTransferList'

export type IncompleteUpload = {
  summary: FileTransferSummary
  plan: StoredUploadPlan
}

/**
 * The sender's drafts that can be continued: its transfers whose file never finished uploading, kept
 * only when the server holds a plan for continuing them. One without a plan, such as one a system
 * initialized over the API, has nothing a browser can carry on with.
 */
export function useIncompleteUploads(party: SelectedParty) {
  const list = useFileTransferList('draft-file-transfers', getDraftFileTransfers, party)

  const plans = useQueries({
    queries: list.items.map((summary) => ({
      queryKey: ['upload-plan', summary.fileTransferId],
      queryFn: () => getUploadPlan(summary.fileTransferId),
    })),
  })

  const uploads: IncompleteUpload[] = list.items.flatMap((summary, index) => {
    const plan = plans[index]?.data
    return plan ? [{ summary, plan }] : []
  })

  return {
    uploads,
    resources: list.resources,
    isLoading: list.isLoading || plans.some((plan) => plan.isPending),
    isError: list.isError,
    hasNextPage: list.hasNextPage,
    isFetchingNextPage: list.isFetchingNextPage,
    fetchNextPage: list.fetchNextPage,
  }
}
