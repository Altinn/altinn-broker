import { useTranslation } from 'react-i18next'
import { getHistoricalFileTransfers } from '../api/historicalFileTransfers'
import { FileTransferList } from '../components/FileTransfersPages/FileTransferList'
import { historicalTransferPath } from './routes'
import { useParties } from '../parties/PartiesContext'
import { SelectedPartyMessage } from '../parties/SelectedPartyMessage'

export function HistoricalFileTransfersPage() {
  const { t } = useTranslation()
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText={t('page.list.historical_loading')} />
  }

  return (
    <FileTransferList
      // A fresh list per actor, instead of unpicking which request belonged to whom.
      key={selectedParty.partyUuid}
      queryKey="historical-file-transfers"
      heading={t('page.list.historical_heading')}
      loadingText={t('page.list.historical_loading')}
      loadErrorText={t('page.list.historical_error')}
      emptyStateText={t('page.list.historical_empty')}
      currentOrg={selectedParty}
      fetchTransfers={getHistoricalFileTransfers}
      toPath={historicalTransferPath}
    />
  )
}
