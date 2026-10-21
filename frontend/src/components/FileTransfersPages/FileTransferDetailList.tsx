import { List } from '@altinn/altinn-components'
import { DetailField } from '../DetailField'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { formatOrganizationDisplay } from '../../helpers/orgIdentifierHelper'
import { formatFileTransferStatusMessage } from '../../helpers/fileTransferStatusHelper'
import type { FileTransferDetails, RecipientDetail } from '../../api/fileTransferDetail'

/** A missing checksum means Broker is still calculating it after upload. */
function formatChecksum(checksum: string | null | undefined, algorithm: string | null | undefined): string {
  const value = checksum?.trim()
  if (!value) {
    return 'Behandles'
  }

  return algorithm ? `${algorithm}: ${value}` : value
}

type FileTransferDetailListProps = {
  transferDetails: FileTransferDetails
}

export function FileTransferDetailList({ transferDetails }: FileTransferDetailListProps) {
  return (
    <List className="data-list">
      <DetailField label="Filnavn" value={transferDetails.fileName} />
      <DetailField label="FormidlingsId" value={transferDetails.fileTransferId} />
      <DetailField label="Referanse" value={transferDetails.sendersFileTransferReference} />
      <DetailField
        label="Avsender"
        value={transferDetails.sender && formatOrganizationDisplay(transferDetails.senderName ?? transferDetails.sender, transferDetails.sender)}
      />
      <DetailField
        label={(transferDetails.recipients?.length ?? 0) > 1 ? 'Mottakere' : 'Mottaker'}
        value={(transferDetails.recipients ?? [])
          .map((recipient: RecipientDetail) => formatOrganizationDisplay(recipient.recipientName ?? recipient.recipient, recipient.recipient))
          .join(', ')}
      />
      <DetailField label="Opprettet" value={transferDetails.created} />
      <DetailField label="Opplastet" value={transferDetails.published} />
      <DetailField label="Nedlastingsfrist" value={transferDetails.expirationTime} />
      <DetailField label="Filstørrelse" value={formatFileSize(transferDetails.fileTransferSize)} />
      <DetailField label="Sjekksum" value={formatChecksum(transferDetails.checksum, transferDetails.checksumAlgorithm)} />
      <DetailField label="Virusskannet" value={transferDetails.useVirusScan ? "Utført" : "Ikke utført"} />
      <DetailField label="Andre metadata" value={Object.entries(transferDetails.propertyList ?? {})
        .map(([key, value]) => `${key}: ${value}`)
        .join(', ')} />
      <DetailField
        label="Status"
        value={formatFileTransferStatusMessage(transferDetails.status, transferDetails.actorDownloadStatus)}
      />
    </List>
  )
}
