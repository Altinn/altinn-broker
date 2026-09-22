import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const CONFIRMDOWNLOAD_FILETRANSFER_PATH = `${BROKER_API_PREFIX}/filetransfer/{fileTransferId}/confirmdownload`

export function confirmFileTransferDownload(fileTransferId: string, onBehalfOf?: SelectedParty): Promise<void> {
    const params = new URLSearchParams()
    if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
    const path = CONFIRMDOWNLOAD_FILETRANSFER_PATH.replace('{fileTransferId}', fileTransferId)
    return apiFetch<void>(params.toString() ? `${path}?${params.toString()}` : path, {
        method: 'POST',
    })
}
