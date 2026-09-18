import { Link, useParams } from 'react-router-dom'
import { FileTransferDetailList } from '../components/FileTransfersPages/FileTransferDetailList'
import { FileTransferActions } from '../components/FileTransfersPages/FileTransferActions'
import { getActiveFileTransferDetails, type ActiveFileTransferDetails } from '../api/activeFileTransferDetail'
import { PageRoutes } from './routes'
import './pages.css'
import { useEffect, useRef, useState } from 'react'
import { List, ListItem } from '@altinn/altinn-components'

const current_org = "312936496"

export function ActiveFileTransferDetailPage() {
  const { transferId = '' } = useParams()
  const [transferDetails, setTransferDetails] = useState<ActiveFileTransferDetails | null>(null)
  const transferIdRef = useRef(transferId)
  transferIdRef.current = transferId

  async function loadTransferDetails() {
    const requestedTransferId = transferId
    try {
      const transferDetails = await getActiveFileTransferDetails(requestedTransferId, current_org)
      if (transferIdRef.current === requestedTransferId) {
        setTransferDetails(transferDetails)
      }
    } catch (error) {
      console.error('Error fetching transfer details:', error)
    }
  }

  useEffect(() => {
    if (transferId) {
      void loadTransferDetails()
    }
  }, [transferId])

  if (!transferId) {
    return <p>Ingen formidling valgt.</p>
  }

  if (!transferDetails) {
    return <p>Fant ikke formidlingen.</p>
  }

  return (
    <div className="page">
      <div className="page-actions">
        <Link to={PageRoutes.active} className="button button--secondary">
          ← Tilbake
        </Link>
      </div>

      <section className="page-section">
        <div>Navn og eier</div>
        <List className="service-owner-list">
          <ListItem
            className="service-owner-list-item"
            icon={{ name: transferDetails.serviceOwner ?? 'serviceOwner', type: 'company' }}
            title={transferDetails.resourceName}
            interactive={false}
            description={transferDetails.serviceOwner}
          />
        </List>

        <FileTransferDetailList transferDetails={transferDetails} />

        <FileTransferActions
          transferDetails={transferDetails}
          onBehalfOf={current_org}
          onDownloadConfirmed={loadTransferDetails}
          onDownloadStarted={loadTransferDetails}
        />
      </section>
    </div>
  )
}
