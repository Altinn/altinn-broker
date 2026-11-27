import { Heading } from '@altinn/altinn-components'
import { Alert } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import {
  BROKERBOX_CONFIGURE_GATEKEEPER_RESOURCE_ID,
  messageWithOptionalInlineLink,
  tjenesteoversiktenResourceUrl,
} from '../../i18n/apiErrors'

/** Formidling is not set up, and publish on the gatekeeper resource is required to do it. */
export function ServiceOwnerSetupNotice() {
  const { t } = useTranslation()
  const parts = messageWithOptionalInlineLink({
    message: t('page.service.owner_not_configured_message'),
    linkHref: tjenesteoversiktenResourceUrl(BROKERBOX_CONFIGURE_GATEKEEPER_RESOURCE_ID),
    linkLabel: BROKERBOX_CONFIGURE_GATEKEEPER_RESOURCE_ID,
    linkInMessage: true,
  })

  return (
    <Alert data-color="warning">
      <Heading as="h2" size="sm">
        {t('page.service.owner_not_configured_heading')}
      </Heading>
      <p>
        {parts.before}
        {parts.link && (
          <a href={parts.link.href} target="_blank" rel="noopener noreferrer">
            {parts.link.label}
          </a>
        )}
        {parts.after}
      </p>
    </Alert>
  )
}
