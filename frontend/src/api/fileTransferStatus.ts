import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

// Service to read and process the status of file transfers.

// Statuses a transfer only gets after its file is uploaded.
const RECEIVED_STATUSES = ['UploadProcessing', 'Published', 'AllConfirmedDownloaded', 'Purged']

/**
 * Whether the server already has the transfer's file. Broker rejects uploads once it does, so a
 * rejected upload may have succeeded with only the response lost. Throws when the status can't be
 * read, since neither answer is safe then.
 */
export async function hasReceivedFile(fileTransferId: string): Promise<boolean> {
  const overview = await apiFetch<{ fileTransferStatus?: string }>(
    `${BROKER_API_PREFIX}/filetransfer/${fileTransferId}`,
  )
  return RECEIVED_STATUSES.includes(overview.fileTransferStatus ?? '')
}
