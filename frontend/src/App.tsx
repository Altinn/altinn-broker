import { Navigate, Route, Routes } from 'react-router-dom'
import { ActiveFileTransferDetailPage } from './pages/ActiveFileTransferDetailPage'
import { ActiveFileTransfersPage } from './pages/ActiveFileTransfersPage'
import { FileTransferServiceDetailPage } from './pages/FileTransferServiceDetailPage'
import { FileTransferServicesPage } from './pages/FileTransferServicesPage'
import { FileTransfersLayout } from './pages/FileTransfersLayout'
import { FileTransfersMainPage } from './pages/FileTransfersMainPage'
import { HistoricalFileTransferDetailPage } from './pages/HistoricalFileTransferDetailPage'
import { HistoricalFileTransfersPage } from './pages/HistoricalFileTransfersPage'
import { NewFileTransferPage } from './pages/NewFileTransferPage'
import { PageRoutes } from './pages/routes'
import './App.css'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to={PageRoutes.fileTransfers} replace />} />
      <Route element={<FileTransfersLayout />}>
        <Route path={PageRoutes.fileTransfers} element={<FileTransfersMainPage />} />
        <Route path={PageRoutes.services} element={<FileTransferServicesPage />} />
        <Route path={PageRoutes.serviceDetail} element={<FileTransferServiceDetailPage />} />
        <Route path={PageRoutes.newFileTransfer} element={<NewFileTransferPage />} />
        <Route path={PageRoutes.active} element={<ActiveFileTransfersPage />} />
        <Route path={PageRoutes.activeDetail} element={<ActiveFileTransferDetailPage />} />
        <Route path={PageRoutes.historical} element={<HistoricalFileTransfersPage />} />
        <Route path={PageRoutes.historicalDetail} element={<HistoricalFileTransferDetailPage />} />
      </Route>
    </Routes>
  )
}

export default App
