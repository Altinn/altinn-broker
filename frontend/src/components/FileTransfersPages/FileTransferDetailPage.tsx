import { Link, type LinkProps, useNavigate, useParams } from 'react-router-dom'
import { Button, List, ListItem } from '@altinn/altinn-components'
import { useEffect, useRef, useState } from 'react'
import { FileTransferDetailList } from './FileTransferDetailList'
import { FileTransferActions } from './FileTransferActions'
import { getFileTransferDetails, type FileTransferDetails } from '../../api/fileTransferDetail'
import { useParties } from '../../parties/PartiesContext'
import '../../pages/pages.css'
import { ArrowUndoIcon } from '@navikt/aksel-icons'

type FileTransferDetailPageProps = {
  backPath: string
  showActions?: boolean
}

export function FileTransferDetailPage({ backPath, showActions = false }: FileTransferDetailPageProps) {
  const { transferId = '' } = useParams()
  const { selectedParty } = useParties()
  const navigate = useNavigate()
  const [transferDetails, setTransferDetails] = useState<FileTransferDetails | null>(null)
  const [loadError, setLoadError] = useState(false)
  const transferIdRef = useRef(transferId)
  transferIdRef.current = transferId
  const previousPartyUuidRef = useRef(selectedParty?.partyUuid)

  async function loadTransferDetails() {
    if (!selectedParty) {
      return
    }
    const requestedTransferId = transferId
    try {
      const transferDetails = await getFileTransferDetails(requestedTransferId, selectedParty)
      if (transferIdRef.current === requestedTransferId) {
        setTransferDetails(transferDetails)
        setLoadError(false)
      }
    } catch (error) {
      console.error('Error fetching transfer details:', error)
      if (transferIdRef.current === requestedTransferId) {
        setLoadError(true)
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
    return <p>Ingen aktør valgt.</p>
  }

  if (loadError) {
    return <p>Fant ikke formidlingen.</p>
  }

  if (!transferDetails) {
    return <p>Laster …</p>
  }

  return (
    <div className="page">
      <div className="page-actions">
        <Button as={(props: LinkProps) => <Link {...props} to={backPath} />} variant="secondary" size="sm">
          <ArrowUndoIcon aria-hidden />
          Tilbake
        </Button>
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
            onBehalfOf={selectedParty}
            onDownloadConfirmed={loadTransferDetails}
            onDownloadStarted={loadTransferDetails}
          />
        )}
      </section>
    </div>
  )
}
