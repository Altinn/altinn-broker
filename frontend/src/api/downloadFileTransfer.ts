import { apiUrl, BROKER_API_PREFIX } from './config'

const DOWNLOAD_FILETRANSFER_PATH = `${BROKER_API_PREFIX}/filetransfer/{fileTransferId}/download`

export function getFileTransferDownloadUrl(
    fileTransferId: string,
    onBehalfOf?: string,
): string {
    const path = DOWNLOAD_FILETRANSFER_PATH.replace('{fileTransferId}', fileTransferId)
    const params = new URLSearchParams()
    if (onBehalfOf) params.set('onBehalfOf', onBehalfOf)
    return params.toString() ? apiUrl(`${path}?${params.toString()}`) : apiUrl(path)
}