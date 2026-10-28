import type { SelectedParty } from '../parties/PartiesContext'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

const ACTIVE_FILETRANSFER_DETAILS_PATH = `${BROKER_API_PREFIX}/frontend/file-transfer-details/{fileTransferId}`

export type NotificationDeliveryStatus =
    | 'Pending'
    | 'Ordered'
    | 'Rejected'
    | 'Sent'
    | 'NotDelivered'
    | 'ConditionNotMet'
    | 'OrderUnconfirmed'

export type NotificationReminderDetail = {
    status: NotificationDeliveryStatus
    time?: string | null
}

export type RecipientNotificationDetail = {
    status: NotificationDeliveryStatus
    sentTime?: string | null
    statusReason?: string | null
    reminder?: NotificationReminderDetail | null
    customRecipientCount: number
    customRecipientsNotifiedCount: number
}

export type RecipientDetail = {
    recipient: string
    recipientName?: string
    // Null when no notification was ordered for the recipient, and - for a recipient viewing - on the other recipients.
    notification?: RecipientNotificationDetail | null
}

export type FileTransferDetails = {
    fileTransferId?: string
    resourceId?: string
    resourceName?: string
    fileName?: string
    sendersFileTransferReference?: string
    useVirusScan?: boolean
    fileTransferSize?: number
    checksum?: string | null
    checksumAlgorithm?: 'MD5' | 'SHA256' | null
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
    notification?: NotificationDetails | null
}

export type NotificationDetails = {
    enabled: boolean
    sendReminder: boolean
    customRecipients: { identifier: string; relatedOrganizationNumber: string }[]

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

