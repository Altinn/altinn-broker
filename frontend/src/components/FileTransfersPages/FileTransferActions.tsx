import { useState } from 'react'
import type { FileTransferDetails } from '../../api/fileTransferDetail'
import type { SelectedParty } from '../../parties/PartiesContext'
import { confirmFileTransferDownload } from '../../api/confirmFileTransferDownload'
import { getFileTransferDownloadUrl } from '../../api/downloadFileTransfer'

const REFRESH_AFTER_DOWNLOAD_START_DELAY_MS = 1500

type FileTransferActionsProps = {
  transferDetails: FileTransferDetails
  onBehalfOf?: SelectedParty
  onDownloadConfirmed?: () => void
  onDownloadStarted?: () => void
}

export function FileTransferActions({ transferDetails, onBehalfOf, onDownloadConfirmed, onDownloadStarted }: FileTransferActionsProps) {
  const [isConfirming, setIsConfirming] = useState(false)

  const isSender = transferDetails.isSender === true
  const canConfirmDownload = transferDetails.actorDownloadStatus === 'DownloadStarted'
  const isDownloadConfirmed = transferDetails.actorDownloadStatus === 'DownloadConfirmed'

  async function handleConfirmDownload() {
    if (!transferDetails.fileTransferId) {
      return
    }
    setIsConfirming(true)
    try {
      await confirmFileTransferDownload(transferDetails.fileTransferId, onBehalfOf)
      onDownloadConfirmed?.()
    } catch (error) {
      console.error('Error confirming download:', error)
    } finally {
      setIsConfirming(false)
    }
  }

  function handleStartDownloadClick() {
    // The browser handles the actual download navigation - don't preventDefault.
    // The backend records DownloadStarted before it streams any bytes, so a short
    // delay is enough to pick up the updated status on refetch.
    setTimeout(() => {
      onDownloadStarted?.()
    }, REFRESH_AFTER_DOWNLOAD_START_DELAY_MS)
  }

  return (
    <>
      {(!isSender || canConfirmDownload || isDownloadConfirmed) && (
        <div className="action-row">
          {!isSender && transferDetails.fileTransferId && (
            <a
              className="button"
              href={getFileTransferDownloadUrl(transferDetails.fileTransferId, onBehalfOf)}
              onClick={handleStartDownloadClick}
            >
              Start nedlasting
            </a>
          )}
          {canConfirmDownload && (
            <button type="button" className="button" disabled={isConfirming} onClick={handleConfirmDownload}>
              Bekreft nedlasting
            </button>
          )}
          {isDownloadConfirmed && (
            <button type="button" className="button" disabled>
              Nedlasting bekreftet
            </button>
          )}
        </div>
      )}

      {/* {isSender && (
        <div className="action-row">
          <button type="button" className="button">
            Kanseller formidling
          </button>
        </div>
      )} */}
    </>
  )
}
