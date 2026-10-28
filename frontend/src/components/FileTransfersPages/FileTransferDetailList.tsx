import { List } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { DetailField } from '../DetailField'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { formatOrganizationDisplay } from '../../helpers/orgIdentifierHelper'
import { formatFileTransferStatusMessage } from '../../helpers/fileTransferStatusHelper'
// import { i18n } from '../../i18n/config'
import type { FileTransferDetails, RecipientDetail } from '../../api/fileTransferDetail'
import { NotificationDetail } from './NotificationDetail'

/** A missing checksum means Broker is still calculating it after upload. */
// function formatChecksum(checksum: string | null | undefined, algorithm: string | null | undefined): string {
//   const value = checksum?.trim()
//   if (!value) {
//     return i18n.t('page.detail.processing')
//   }
//
//   return algorithm ? `${algorithm}: ${value}` : value
// }

type FileTransferDetailListProps = {
  transferDetails: FileTransferDetails
}

export function FileTransferDetailList({ transferDetails }: FileTransferDetailListProps) {
  const { t } = useTranslation()
  return (
    <List className="data-list">
      <DetailField label={t('page.detail.file_name')} value={transferDetails.fileName} />
      <DetailField label={t('page.detail.transfer_id')} value={transferDetails.fileTransferId} />
      <DetailField label={t('page.common.reference')} value={transferDetails.sendersFileTransferReference} />
      <DetailField
        label={t('page.common.sender')}
        value={transferDetails.sender && formatOrganizationDisplay(transferDetails.senderName ?? transferDetails.sender, transferDetails.sender)}
      />
      <DetailField
        label={(transferDetails.recipients?.length ?? 0) > 1 ? t('page.common.recipients') : t('page.common.recipient')}
        value={(transferDetails.recipients ?? [])
          .map((recipient: RecipientDetail) => formatOrganizationDisplay(recipient.recipientName ?? recipient.recipient, recipient.recipient))
          .join(', ')}
      />
      <NotificationDetail transferDetails={transferDetails} />
      <DetailField label={t('page.detail.created')} value={transferDetails.created} />
      <DetailField label={t('page.detail.uploaded')} value={transferDetails.published} />
      <DetailField label={t('page.detail.deadline')} value={transferDetails.expirationTime} />
      <DetailField label={t('page.detail.file_size')} value={formatFileSize(transferDetails.fileTransferSize)} />
      {/* Checksum hidden for now; restore by uncommenting.
      <DetailField label={t('page.detail.checksum')} value={formatChecksum(transferDetails.checksum, transferDetails.checksumAlgorithm)} />
      */}
      <DetailField label={t('page.detail.virus_scanned')} value={transferDetails.useVirusScan ? t('page.detail.virus_done') : t('page.detail.virus_not_done')} />
      <DetailField label={t('page.detail.metadata')} value={Object.entries(transferDetails.propertyList ?? {})
        .map(([key, value]) => `${key}: ${value}`)
        .join(', ')} />
      <DetailField
        label={t('page.detail.status')}
        value={formatFileTransferStatusMessage(transferDetails.status, transferDetails.actorDownloadStatus)}
      />
    </List>
  )
}
