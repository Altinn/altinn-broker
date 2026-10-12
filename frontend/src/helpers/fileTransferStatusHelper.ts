import type { DialogStatusProps } from '@altinn/altinn-components'
import type { FileTransferSummary } from '../api/fileTransferSummary'

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
      return { status: { value: 'awaiting', label: 'Til behandling' }, extendedStatusLabel: 'Filen behandles' }
    case 'Published':
      return getPublishedListStatus(summary.actorDownloadStatus)
    case 'AllConfirmedDownloaded':
      return getEndedListStatus('Alle mottakere har lastet ned')
    case 'Cancelled':
      return getEndedListStatus('Kansellert')
    case 'Purged':
      return getEndedListStatus('Filen er slettet')
    case 'Failed':
      return getEndedListStatus('Feilet')
    default:
      return undefined
  }
}

function getPublishedListStatus(actorDownloadStatus: FileTransferSummary['actorDownloadStatus']): FileTransferListStatus {
  switch (actorDownloadStatus) {
    case 'Initialized':
      return { status: { value: 'requires-attention', label: 'Krever handling' }, extendedStatusLabel: 'Last ned filen' }
    case 'DownloadStarted':
      return { status: { value: 'requires-attention', label: 'Krever handling' }, extendedStatusLabel: 'Bekreft nedlasting' }
    case 'DownloadConfirmed':
      return { status: { value: 'completed', label: 'Lastet ned' }, extendedStatusLabel: 'Venter på andre mottakere' }
    default:
      return { status: { value: 'awaiting', label: 'Sendt' }, extendedStatusLabel: 'Venter på at mottaker laster ned' }
  }
}

function getEndedListStatus(extendedStatusLabel: string): FileTransferListStatus {
  return { status: { value: 'completed', label: 'Avsluttet' }, extendedStatusLabel }
}

/** Maps the backend's `status` field to the message shown in the transfer's status detail row. */
export function formatFileTransferStatusMessage(
  status: string | undefined,
  actorDownloadStatus?: string | null,
): string {
  switch (status) {
    case 'AwaitingDownloadByCurrentActor':
      return actorDownloadStatus === 'DownloadStarted'
        ? 'Nedlasting startet. Bekreft nedlasting for sluttstatus.'
        : 'Venter på nedlasting.'
    case 'AwaitingOtherRecipients':
      return 'Venter på nedlasting av resterende mottakere.'
    case 'AwaitingRecipients':
      return 'Venter på at mottaker laster ned filen.'
    case 'AllDownloaded':
      return 'Alle mottakere har lastet ned filen.'
    case 'Cancelled':
      return 'Formidlingen er kansellert.'
    case 'Purged':
      return 'Filen er slettet.'
    case 'Failed':
      return 'Formidlingen feilet.'
    default:
      return status ?? ''
  }
}
