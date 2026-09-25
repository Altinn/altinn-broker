import { getActiveFileTransfers } from '../api/activeFileTransfers'
import { FileTransferList } from '../components/FileTransfersPages/FileTransferList'
import { activeTransferPath } from './routes'
import { useParties } from '../parties/PartiesContext'
import { SelectedPartyMessage } from '../parties/SelectedPartyMessage'

export function ActiveFileTransfersPage() {
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText="Laster aktive formidlinger …" />
  }

  return (
    <FileTransferList
      heading="Aktive formidlinger"
      loadingText="Laster aktive formidlinger …"
      loadErrorText="Klarte ikke å hente aktive formidlinger."
      emptyStateText="Ingen aktive formidlinger funnet."
      currentOrg={selectedParty}
      fetchTransfers={getActiveFileTransfers}
      toPath={activeTransferPath}
    />
  )
}
