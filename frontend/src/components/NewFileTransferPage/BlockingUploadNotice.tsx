import { Alert, Button, Heading, Paragraph } from '@digdir/designsystemet-react'
import { useState } from 'react'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { useUploadProgress, type ActiveUpload } from '../../upload/uploadsContext'

type BlockingUploadNoticeProps = {
  upload: ActiveUpload
  onContinue: () => void
  onCancel: () => void
}

export function BlockingUploadNotice({ upload, onContinue, onCancel }: BlockingUploadNoticeProps) {
  const [confirming, setConfirming] = useState(false)
  const percent = useUploadProgress()?.percent ?? null

  return (
    <Alert data-color="info" className="new-transfer__notice">
      <Heading level={3} data-size="2xs">
        {heading(upload)}
      </Heading>
      <Paragraph data-size="sm">
        «{upload.fileName}» ({formatFileSize(upload.fileSize)}){progressText(upload, percent)}. Du
        kan laste opp én fil om gangen, så denne må fullføres eller avbrytes før du sender en ny.
      </Paragraph>
      {confirming ? (
        <>
          <Paragraph data-size="sm">
            Avbryter du, går det som er lastet opp tapt og filen må sendes på nytt.
          </Paragraph>
          <div className="new-transfer__resume-actions">
            <Button type="button" variant="primary" data-color="danger" data-size="sm" onClick={onCancel}>
              Ja, avbryt opplastingen
            </Button>
            <Button
              type="button"
              variant="tertiary"
              data-size="sm"
              onClick={() => setConfirming(false)}
            >
              Nei, behold den
            </Button>
          </div>
        </>
      ) : (
        <div className="new-transfer__resume-actions">
          <Button type="button" variant="secondary" data-size="sm" onClick={onContinue}>
            Gå til opplastingen
          </Button>
          <Button
            type="button"
            variant="tertiary"
            data-size="sm"
            onClick={() => setConfirming(true)}
          >
            Avbryt den opplastingen
          </Button>
        </div>
      )}
    </Alert>
  )
}

function progressText(upload: ActiveUpload, percent: number | null): string {
  if (percent !== null) {
    return ` er ${percent} % lastet opp`
  }
  return upload.status === 'initializing' ? ' er ikke kommet i gang ennå' : ' er ikke fullført'
}

function heading(upload: ActiveUpload): string {
  switch (upload.status) {
    case 'failed':
      return 'En annen opplasting stoppet'
    case 'pausing':
    case 'paused':
      return 'En annen opplasting er satt på pause'
    default:
      return 'En annen opplasting pågår'
  }
}
