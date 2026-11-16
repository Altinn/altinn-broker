import { ListItem } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import type { FileTransferDetails } from '../../api/fileTransferDetail'
import { getDownloadStatusTag } from '../../helpers/downloadStatusHelper'
import { RecipientStatusList, type RecipientStatus } from './RecipientStatusList'

type DownloadDetailProps = {
  transferDetails: FileTransferDetails
}

/**
 * The "Nedlasting" row of the detail list: how far each recipient the viewer may see - every recipient for the
 * sender, only its own for a recipient - has come with downloading the file, and when it confirmed the download.
 */
export function DownloadDetail({ transferDetails }: DownloadDetailProps) {
  const { t } = useTranslation()
  const statuses: RecipientStatus[] = (transferDetails.recipients ?? []).flatMap(({ recipient, recipientName, download }) =>
    download
      ? [{ recipient, recipientName, tag: getDownloadStatusTag(download, t) }]
      : [],
  )
  if (statuses.length === 0) {
    return null
  }

  return (
    <ListItem
      className="data-list-item"
      shadow="none"
      interactive={false}
      title={t('page.detail.download')}
      description={<RecipientStatusList statuses={statuses} />}
    />
  )
}
