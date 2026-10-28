import { useTranslation } from 'react-i18next'
import { getActiveFileTransfers } from '../api/activeFileTransfers'
import { FileTransferList } from '../components/FileTransfersPages/FileTransferList'
import { activeTransferPath } from './routes'
import { useParties } from '../parties/PartiesContext'
import { SelectedPartyMessage } from '../parties/SelectedPartyMessage'

export function ActiveFileTransfersPage() {
  const { t } = useTranslation()
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText={t('page.list.active_loading')} />
  }

  return (
    <FileTransferList
      // A fresh list per actor, instead of unpicking which request belonged to whom.
      key={selectedParty.partyUuid}
      queryKey="active-file-transfers"
      heading={t('page.list.active_heading')}
      loadingText={t('page.list.active_loading')}
      loadErrorText={t('page.list.active_error')}
      emptyStateText={t('page.list.active_empty')}
      currentOrg={selectedParty}
      fetchTransfers={getActiveFileTransfers}
      toPath={activeTransferPath}
      showDeadline
    />
  )
}
