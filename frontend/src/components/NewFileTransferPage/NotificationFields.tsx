import { Field, Switch } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'

type NotificationFieldsProps = {
  sendReminder: boolean
  onSendReminderChange: (sendReminder: boolean) => void
}

/**
 * How the recipients are notified about the file transfer. The text itself is Altinn's standard message.
 */
export function NotificationFields({ sendReminder, onSendReminderChange }: NotificationFieldsProps) {
  const { t } = useTranslation()
  return (
    <>

      <Field>
        <Switch
          label={t('page.notify.reminder')}
          checked={sendReminder}
          onChange={(event) => onSendReminderChange(event.target.checked)}
        />
        <Field.Description>{t('page.notify.reminder_description')}</Field.Description>
      </Field>
    </>
  )
}
