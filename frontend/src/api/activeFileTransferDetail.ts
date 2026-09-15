import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const ACTIVE_FILETRANSFER_DETAILS_PATH = `${BROKER_API_PREFIX}/frontend/active-file-transfer/{fileTransferId}`

export type RecipientDetail = {
    recipient: string
    recipientName?: string
}

export type ActiveFileTransferDetails = {
    fileTransferId?: string
    resourceId?: string
    fileName?: string
    sendersFileTransferReference?: string
    useVirusScan?: boolean
    fileTransferSize?: string
    created?: string | null
    sender?: string
    senderName?: string
    recipients?: RecipientDetail[]
    propertyList?: Record<string, string>
    published?: string | null
    serviceOwner?: string
    expirationTime?: string | null
    status?: string
    isSender?: boolean
    actorDownloadStatus?: 'Initialized' | 'DownloadStarted' | 'DownloadConfirmed' | null
}

export function getActiveFileTransferDetails(
    fileTransferId: string,
    onBehalfOf?: string,
): Promise<ActiveFileTransferDetails> {
    const params = new URLSearchParams()
    if (onBehalfOf) params.set('onBehalfOf', onBehalfOf)
    return apiFetch<ActiveFileTransferDetails>(
        ACTIVE_FILETRANSFER_DETAILS_PATH.replace('{fileTransferId}', fileTransferId) + `?${params.toString()}`,
        { redirectOnUnauthorized: false },
    )
}

