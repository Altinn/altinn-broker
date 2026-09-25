import { Link, useNavigate, useParams } from 'react-router-dom'
import { List, ListItem } from '@altinn/altinn-components'
import { useEffect, useRef, useState } from 'react'
import { FileTransferDetailList } from './FileTransferDetailList'
import { FileTransferActions } from './FileTransferActions'
import { getFileTransferDetails, type FileTransferDetails } from '../../api/fileTransferDetail'
import { ApiError } from '../../api/client'
import { useParties } from '../../parties/PartiesContext'
import { SelectedPartyMessage } from '../../parties/SelectedPartyMessage'
import '../../pages/pages.css'

type FileTransferDetailPageProps = {
  backPath: string
  showActions?: boolean
}

export function FileTransferDetailPage({ backPath, showActions = false }: FileTransferDetailPageProps) {
  const { transferId = '' } = useParams()
  const { selectedParty } = useParties()
  const navigate = useNavigate()
  const [transferDetails, setTransferDetails] = useState<FileTransferDetails | null>(null)
  const [loadError, setLoadError] = useState<'missing' | 'failed' | null>(null)
  const transferIdRef = useRef(transferId)
  transferIdRef.current = transferId
  const selectedPartyUuidRef = useRef(selectedParty?.partyUuid)
  selectedPartyUuidRef.current = selectedParty?.partyUuid
  const previousPartyUuidRef = useRef(selectedParty?.partyUuid)

  async function loadTransferDetails() {
    if (!selectedParty) {
      return
    }
    const requestedTransferId = transferId
    const requestedPartyUuid = selectedParty.partyUuid
    try {
      const transferDetails = await getFileTransferDetails(requestedTransferId, selectedParty)
      if (transferIdRef.current === requestedTransferId && selectedPartyUuidRef.current === requestedPartyUuid) {
        setTransferDetails(transferDetails)
        setLoadError(null)
      }
    } catch (error) {
      console.error('Error fetching transfer details:', error)
      if (transferIdRef.current === requestedTransferId && selectedPartyUuidRef.current === requestedPartyUuid) {
        // Only a 404 means the transfer is gone. Anything else is worth retrying.
        setLoadError(error instanceof ApiError && error.status === 404 ? 'missing' : 'failed')
      }
    }
  }

  useEffect(() => {
    const previousPartyUuid = previousPartyUuidRef.current
    previousPartyUuidRef.current = selectedParty?.partyUuid

    if (previousPartyUuid && selectedParty && previousPartyUuid !== selectedParty.partyUuid) {
      navigate(backPath)
      return
    }

    if (transferId && selectedParty) {
      void loadTransferDetails()
    }
  }, [transferId, selectedParty, backPath, navigate])

  if (!transferId) {
    return <p>Ingen formidling valgt.</p>
  }

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText="Laster formidlingen …" />
  }

  if (loadError === 'missing') {
    return <p className="empty-state">Fant ikke formidlingen.</p>
  }

  if (loadError === 'failed') {
    return (
      <p className="empty-state">Klarte ikke å hente formidlingen. Prøv igjen senere.</p>
    )
  }

  if (!transferDetails) {
    return <p>Laster …</p>
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
            icon={{ name: transferDetails.serviceOwner ?? transferDetails.resourceName ?? '', type: 'company' }}
            title={transferDetails.resourceName}
            interactive={false}
            description={transferDetails.serviceOwner}
          />
        </List>

        <FileTransferDetailList transferDetails={transferDetails} />

        {showActions && (
          <FileTransferActions
            transferDetails={transferDetails}
            onBehalfOf={selectedParty}
            onDownloadConfirmed={loadTransferDetails}
            onDownloadStarted={loadTransferDetails}
          />
        )}
      </section>
    </div>
  )
}
