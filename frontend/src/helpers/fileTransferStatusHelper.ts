import type { DialogStatusProps } from '@altinn/altinn-components'
import type { FileTransferSummary } from '../api/fileTransferSummary'
import { i18n } from '../i18n/config'

export type FileTransferListStatus = {
  status: DialogStatusProps
  extendedStatusLabel: string
}

/**
 * Where a listed transfer stands for the viewer, shown the way arbeidsflate shows a dialog's status:
 * a general state, then what it means for this transfer. Only a recipient who has not confirmed the
 * download is asked to act.
 */
export function getFileTransferListStatus(
  summary: Pick<FileTransferSummary, 'fileTransferStatus' | 'actorDownloadStatus'>,
): FileTransferListStatus | undefined {
  switch (summary.fileTransferStatus) {
    case 'UploadProcessing':
      return { status: { value: 'awaiting', label: i18n.t('page.status.awaiting') }, extendedStatusLabel: i18n.t('page.status.processing') }
    case 'Published':
      return getPublishedListStatus(summary.actorDownloadStatus)
    case 'AllConfirmedDownloaded':
      return getEndedListStatus(i18n.t('page.status.all_downloaded'))
    case 'Cancelled':
      return getEndedListStatus(i18n.t('page.status.cancelled'))
    case 'Purged':
      return getEndedListStatus(i18n.t('page.status.deleted'))
    case 'Failed':
      return getEndedListStatus(i18n.t('page.status.failed'))
    default:
      return undefined
  }
}

function getPublishedListStatus(actorDownloadStatus: FileTransferSummary['actorDownloadStatus']): FileTransferListStatus {
  switch (actorDownloadStatus) {
    case 'Initialized':
      return { status: { value: 'requires-attention', label: i18n.t('page.status.requires_action') }, extendedStatusLabel: i18n.t('page.status.download') }
    case 'DownloadStarted':
      return { status: { value: 'requires-attention', label: i18n.t('page.status.requires_action') }, extendedStatusLabel: i18n.t('page.status.confirm') }
    case 'DownloadConfirmed':
      return { status: { value: 'completed', label: i18n.t('page.status.downloaded') }, extendedStatusLabel: i18n.t('page.status.waiting_others') }
    default:
      return { status: { value: 'awaiting', label: i18n.t('page.status.sent') }, extendedStatusLabel: i18n.t('page.status.waiting_recipient') }
  }
}

function getEndedListStatus(extendedStatusLabel: string): FileTransferListStatus {
  return { status: { value: 'completed', label: i18n.t('page.status.ended') }, extendedStatusLabel }
}

/** Maps the backend's `status` field to the message shown in the transfer's status detail row. */
export function formatFileTransferStatusMessage(
  status: string | undefined,
  actorDownloadStatus?: string | null,
): string {
  switch (status) {
    case 'AwaitingDownloadByCurrentActor':
      return actorDownloadStatus === 'DownloadStarted'
        ? i18n.t('page.status.awaiting_download_started')
        : i18n.t('page.status.awaiting_download')
    case 'AwaitingOtherRecipients':
      return i18n.t('page.status.awaiting_remaining')
    case 'AwaitingRecipients':
      return i18n.t('page.status.awaiting_recipients')
    case 'AllDownloaded':
      return i18n.t('page.status.all_downloaded_file')
    case 'Cancelled':
      return i18n.t('page.status.transfer_cancelled')
    case 'Purged':
      return i18n.t('page.status.file_deleted')
    case 'Failed':
      return i18n.t('page.status.transfer_failed')
    default:
      return status ?? ''
  }
}
