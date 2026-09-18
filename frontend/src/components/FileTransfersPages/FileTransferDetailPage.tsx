import { Link, useParams } from 'react-router-dom'
import { List, ListItem } from '@altinn/altinn-components'
import { useEffect, useRef, useState } from 'react'
import { FileTransferDetailList } from './FileTransferDetailList'
import { FileTransferActions } from './FileTransferActions'
import { getFileTransferDetails, type FileTransferDetails } from '../../api/fileTransferDetail'
import '../../pages/pages.css'

const current_org = "312936496"

type FileTransferDetailPageProps = {
  backPath: string
  showActions?: boolean
}

export function FileTransferDetailPage({ backPath, showActions = false }: FileTransferDetailPageProps) {
  const { transferId = '' } = useParams()
  const [transferDetails, setTransferDetails] = useState<FileTransferDetails | null>(null)
  const transferIdRef = useRef(transferId)
  transferIdRef.current = transferId

  async function loadTransferDetails() {
    const requestedTransferId = transferId
    try {
      const transferDetails = await getFileTransferDetails(requestedTransferId, current_org)
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
        <Link to={backPath} className="button button--secondary">
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

        {showActions && (
          <FileTransferActions
            transferDetails={transferDetails}
            onBehalfOf={current_org}
            onDownloadConfirmed={loadTransferDetails}
            onDownloadStarted={loadTransferDetails}
          />
        )}
      </section>
    </div>
  )
}
