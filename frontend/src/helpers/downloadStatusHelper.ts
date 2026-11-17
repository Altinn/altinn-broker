import type { TFunction } from 'i18next'
import type { RecipientDownloadDetail } from '../api/fileTransferDetail'
import type { RecipientStatusTag } from '../components/FileTransfersPages/RecipientStatusList'
import { formatDateTime } from './dateTimeHelper'

/** Only a confirmation says the recipient received the file; a started download may never have finished. */
export function getDownloadStatusTag(download: RecipientDownloadDetail, t: TFunction): RecipientStatusTag {
  switch (download.status) {
    case 'DownloadConfirmed': {
      const confirmedAt = formatDateTime(download.confirmedAt)
      return {
        label: confirmedAt ? t('page.detail.download_confirmed_at', { time: confirmedAt }) : t('page.detail.download_confirmed'),
        color: 'success',
      }
    }
    case 'DownloadStarted':
      return { label: t('page.detail.download_attempted'), color: 'warning' }
    case 'Initialized':
      return { label: t('page.detail.not_downloaded'), color: 'neutral' }
  }
}
