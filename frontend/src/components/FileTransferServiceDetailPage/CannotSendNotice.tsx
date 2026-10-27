import { Alert } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'

/**
 * Shown when the party can open the service but is not registered for sending
 * (receive-only, or an owned resource without send rights).
 */
export function CannotSendNotice() {
  const { t } = useTranslation()
  return (
    <Alert
      variant="info"
      heading={t('page.service.cannot_send_heading')}
      message={t('page.service.cannot_send_message')}
    />
  )
}
