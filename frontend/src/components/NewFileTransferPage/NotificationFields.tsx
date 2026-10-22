import { Field, Switch } from '@digdir/designsystemet-react'

type NotificationFieldsProps = {
  sendReminder: boolean
  onSendReminderChange: (sendReminder: boolean) => void
}

/**
 * How the recipients are notified about the file transfer. The text itself is Altinn's standard message.
 */
export function NotificationFields({ sendReminder, onSendReminderChange }: NotificationFieldsProps) {
  return (
    <>

      <Field>
        <Switch
          label="Send påminnelse"
          checked={sendReminder}
          onChange={(event) => onSendReminderChange(event.target.checked)}
        />
        <Field.Description>Mottakerne får en påminnelse etter 7 dager.</Field.Description>
      </Field>
    </>
  )
}
