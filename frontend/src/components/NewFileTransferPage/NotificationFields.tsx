import { Field, Fieldset, Radio, Switch } from '@digdir/designsystemet-react'
import type { NotificationChannel } from './formFields'

type NotificationFieldsProps = {
  channel: NotificationChannel
  sendReminder: boolean
  onChannelChange: (channel: NotificationChannel) => void
  onSendReminderChange: (sendReminder: boolean) => void
}

const channelLabels: Record<NotificationChannel, string> = {
  EmailPreferred: 'E-post, eller SMS om e-postadresse mangler',
  SmsPreferred: 'SMS, eller e-post om mobilnummer mangler',
  Email: 'Kun e-post',
  Sms: 'Kun SMS',
  EmailAndSms: 'Både e-post og SMS',
}

/**
 * How the recipients are notified about the file transfer. The text itself is Altinn's standard message.
 */
export function NotificationFields({ channel, sendReminder, onChannelChange, onSendReminderChange }: NotificationFieldsProps) {
  return (
    <>
      <Fieldset>
        <Fieldset.Legend>Varslingskanal</Fieldset.Legend>
        <Fieldset.Description>Hvordan mottakerne skal varsles om formidlingen.</Fieldset.Description>
        {(Object.keys(channelLabels) as NotificationChannel[]).map((option) => (
          <Radio
            key={option}
            name="notification-channel"
            value={option}
            label={channelLabels[option]}
            checked={channel === option}
            onChange={() => onChannelChange(option)}
          />
        ))}
      </Fieldset>

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
