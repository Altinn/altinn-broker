import { historicalTransfers } from '../data/mockData'
import { TransferListItem } from '../components/TransferListItem'
import { historicalTransferPath } from './routes'
import './pages.css'

export function HistoricalFileTransfersPage() {
  return (
    <div className="page">
      <h2 className="page-heading">Historiske formidlinger Brønnøy sykehus har vært delaktig i</h2>

      <ul className="transfer-list">
        {historicalTransfers.map((transfer) => (
          <TransferListItem
            key={transfer.id}
            serviceName={transfer.serviceName}
            subtitle={transfer.subtitle}
            to={historicalTransferPath(transfer.id)}
          />
        ))}
      </ul>
    </div>
  )
}
