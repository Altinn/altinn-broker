import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const ACTIVE_FILETRANSFER_DETAILS_PATH = `${BROKER_API_PREFIX}/frontend/file-transfer-details/{fileTransferId}`

export type RecipientDetail = {
    recipient: string
    recipientName?: string
}

export type FileTransferDetails = {
    fileTransferId?: string
    resourceId?: string
    resourceName?: string
    fileName?: string
    sendersFileTransferReference?: string
    useVirusScan?: boolean
    fileTransferSize?: number
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

export function getFileTransferDetails(
    fileTransferId: string,
    onBehalfOf?: SelectedParty,
): Promise<FileTransferDetails> {
    const params = new URLSearchParams()
    if (onBehalfOf) params.set('onBehalfOf', onBehalfOf.organizationNumber)
    return apiFetch<FileTransferDetails>(
        ACTIVE_FILETRANSFER_DETAILS_PATH.replace('{fileTransferId}', fileTransferId) + `?${params.toString()}`,
        { redirectOnUnauthorized: false },
    )
}

