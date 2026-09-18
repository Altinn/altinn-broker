import { FileTransferDetailPage } from '../components/FileTransfersPages/FileTransferDetailPage'
import { PageRoutes } from './routes'

export function ActiveFileTransferDetailPage() {
  return <FileTransferDetailPage backPath={PageRoutes.active} showActions />
}
