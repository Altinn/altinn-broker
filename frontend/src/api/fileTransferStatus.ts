import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

// Service to read and process the status of file transfers.

// Statuses a transfer only gets after its file is uploaded.
const RECEIVED_STATUSES = ['UploadProcessing', 'Published', 'AllConfirmedDownloaded', 'Purged']

export async function getFileTransferStatus(fileTransferId: string, signal?: AbortSignal): Promise<string | undefined> {
  const overview = await apiFetch<{ fileTransferStatus?: string }>(
    `${BROKER_API_PREFIX}/filetransfer/${fileTransferId}`,
    { signal },
  )
  return overview.fileTransferStatus
}

/**
 * Whether the server already has the transfer's file. Broker rejects uploads once it does, so a
 * rejected upload may have succeeded with only the response lost. Throws when the status can't be
 * read, since neither answer is safe then.
 */
export async function hasReceivedFile(fileTransferId: string): Promise<boolean> {
  return RECEIVED_STATUSES.includes((await getFileTransferStatus(fileTransferId)) ?? '')
}
