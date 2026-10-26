import { Alert } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'

/**
 * Shown in place of the create action when the service has no one the party may send to.
 * Both the service detail page and the form use it, so the wording stays in one place.
 */
export function NoRecipientsNotice() {
  const { t } = useTranslation()
  return (
    <Alert
      variant="info"
      heading={t('page.service.no_recipients_heading')}
      message={t('page.service.no_recipients_message')}
    />
  )
}
