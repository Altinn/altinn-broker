import { Alert, Heading, Paragraph } from '@digdir/designsystemet-react'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type InterruptedUploadNoticeProps = {
  fileName: string
  fileSize: number
  /** Bytes the server already holds. */
  uploaded: number
  ready: boolean
}

export function InterruptedUploadNotice({ fileName, fileSize, uploaded, ready }: InterruptedUploadNoticeProps) {
  const percent = Math.floor((uploaded / fileSize) * 100)

  return (
    <Alert data-color="info" className="new-transfer__resume">
      <Heading level={3} data-size="2xs">
        Avbrutt opplasting
      </Heading>
      <Paragraph data-size="sm">
        {ready
          ? `Opplastingen av «${fileName}» fortsetter der den slapp.`
          : `Opplastingen av «${fileName}» (${formatFileSize(fileSize)}) ble avbrutt. Velg den samme filen under for å fortsette der den slapp. Feltene over ble lagret da opplastingen startet, og kan ikke endres.`}
      </Paragraph>
      <progress value={percent} max={100} aria-label="Allerede lastet opp" />
      <Paragraph data-size="sm">
        {percent} % ({formatFileSize(uploaded)} av {formatFileSize(fileSize)}) er allerede lastet opp, og sendes
        ikke på nytt.
      </Paragraph>
    </Alert>
  )
}
