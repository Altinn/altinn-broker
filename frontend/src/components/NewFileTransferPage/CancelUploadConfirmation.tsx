import { Button, Paragraph } from '@digdir/designsystemet-react'

type CancelUploadConfirmationProps = {
  onConfirm: () => void
  onDismiss: () => void
}

export function CancelUploadConfirmation({ onConfirm, onDismiss }: CancelUploadConfirmationProps) {
  return (
    <>
      <Paragraph data-size="sm">
        Avbryter du, går det som er lastet opp tapt og filen må sendes på nytt.
      </Paragraph>
      <div className="new-transfer__resume-actions">
        <Button type="button" variant="primary" data-color="danger" data-size="sm" onClick={onConfirm}>
          Ja, avbryt opplastingen
        </Button>
        <Button type="button" variant="tertiary" data-size="sm" onClick={onDismiss}>
          Nei, behold den
        </Button>
      </div>
    </>
  )
}
