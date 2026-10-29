import { Alert } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { useParties } from './PartiesContext'

type SelectedPartyMessageProps = {
  /** Shown while the party list is still loading, so the page reads as busy rather than empty. */
  loadingText: string
}

/**
 * What to show in place of a page that needs a selected actor. The three reasons a party can be
 * missing — still loading, the lookup failed, or the user represents no organizations — read very
 * differently to the user, so they are kept apart.
 */
export function SelectedPartyMessage({ loadingText }: SelectedPartyMessageProps) {
  const { t } = useTranslation()
  const { status } = useParties()

  if (status === 'failed') {
    return (
      <div className="page">
        <Alert
          variant="danger"
          heading={t('page.parties.load_failed_heading')}
          message={t('page.parties.load_failed_message')}
        />
      </div>
    )
  }

  if (status === 'loading') {
    return <p className="empty-state">{loadingText}</p>
  }

  return (
    <div className="page">
      <Alert
        variant="info"
        heading={t('page.parties.none_heading')}
        message={t('page.parties.none_message')}
      />
    </div>
  )
}
