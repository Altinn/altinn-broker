import { Alert, Button, Heading, Paragraph } from '@digdir/designsystemet-react'
import type { FileFingerprint } from '../../upload/uploadSession'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type InterruptedUploadNoticeProps = {
  file: FileFingerprint
  /** Bytes the server already holds, or null while that is still being read back. */
  uploaded: number | null
  ready: boolean
  onDiscard: () => void
}

export function InterruptedUploadNotice({
  file,
  uploaded,
  ready,
  onDiscard,
}: InterruptedUploadNoticeProps) {
  const percent = uploaded === null ? null : Math.floor((uploaded / file.size) * 100)

  return (
    <Alert data-color="info" className="new-transfer__resume">
      <Heading level={3} data-size="2xs">
        Avbrutt opplasting
      </Heading>
      <Paragraph data-size="sm">
        {ready
          ? `Opplastingen av «${file.name}» fortsetter der den slapp.`
          : `Opplastingen av «${file.name}» (${formatFileSize(file.size)}) ble avbrutt. Velg den samme filen under for å fortsette der den slapp. Feltene over ble lagret da opplastingen startet, og kan ikke endres.`}
      </Paragraph>
      {percent !== null && uploaded !== null && (
        <>
          <progress value={percent} max={100} aria-label="Allerede lastet opp" />
          <Paragraph data-size="sm">
            {percent} % ({formatFileSize(uploaded)} av {formatFileSize(file.size)}) er allerede
            lastet opp, og sendes ikke på nytt.
          </Paragraph>
        </>
      )}
      <div className="new-transfer__resume-actions">
        <Button type="button" variant="tertiary" data-size="sm" onClick={onDiscard}>
          Forkast
        </Button>
      </div>
    </Alert>
  )
}
