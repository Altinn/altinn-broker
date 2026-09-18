import { getActiveFileTransfers } from '../api/activeFileTransfers'
import { FileTransferList } from '../components/FileTransfersPages/FileTransferList'
import { activeTransferPath } from './routes'

const current_org = "312936496"

export function ActiveFileTransfersPage() {
  return (
    <FileTransferList
      heading="Aktive formidlinger"
      loadingText="Laster aktive formidlinger …"
      loadErrorText="Klarte ikke å hente aktive formidlinger."
      emptyStateText="Ingen aktive formidlinger funnet."
      currentOrg={current_org}
      fetchTransfers={getActiveFileTransfers}
      toPath={activeTransferPath}
    />

  )
}
