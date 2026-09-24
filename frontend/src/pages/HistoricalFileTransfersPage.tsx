import { getHistoricalFileTransfers } from '../api/historicalFileTransfers'
import { FileTransferList } from '../components/FileTransfersPages/FileTransferList'
import { historicalTransferPath } from './routes'
import { useParties } from '../parties/PartiesContext'
import { SelectedPartyMessage } from '../parties/SelectedPartyMessage'

export function HistoricalFileTransfersPage() {
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText="Laster historiske formidlinger …" />
  }

  return (
    <FileTransferList
      heading="Historiske formidlinger"
      loadingText="Laster historiske formidlinger …"
      loadErrorText="Klarte ikke å hente historiske formidlinger."
      emptyStateText="Ingen historiske formidlinger funnet."
      currentOrg={selectedParty}
      fetchTransfers={getHistoricalFileTransfers}
      toPath={historicalTransferPath}
    />
  )
}
