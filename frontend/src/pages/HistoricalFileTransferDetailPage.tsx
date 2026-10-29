import { FileTransferDetailPage } from '../components/FileTransfersPages/FileTransferDetailPage'
import { PageRoutes } from './routes'

export function HistoricalFileTransferDetailPage() {
  return <FileTransferDetailPage backPath={PageRoutes.historical} />
}
