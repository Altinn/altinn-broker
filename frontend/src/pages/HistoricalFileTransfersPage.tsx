import { getHistoricalFileTransfers } from '../api/historicalFileTransfers'
import { FileTransferList } from '../components/FileTransfersPages/FileTransferList'
import { historicalTransferPath } from './routes'

const current_org = "312936496"

export function HistoricalFileTransfersPage() {
  return (
    <FileTransferList
      heading="Historiske formidlinger"
      loadingText="Laster historiske formidlinger …"
      loadErrorText="Klarte ikke å hente historiske formidlinger."
      emptyStateText="Ingen historiske formidlinger funnet."
      currentOrg={current_org}
      fetchTransfers={getHistoricalFileTransfers}
      toPath={historicalTransferPath}
    />
  )
}
