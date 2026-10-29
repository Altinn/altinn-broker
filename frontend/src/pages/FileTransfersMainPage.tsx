import { useTranslation } from 'react-i18next'
import { CardLink } from '../components/CardLink'
import { useParties } from '../parties/PartiesContext'
import { OrganizationHeader } from '../components/OrganizationHeader'
import { IncompleteUploadsBanner } from '../components/FileTransfersPages/IncompleteUploadsBanner'
import { PageRoutes } from './routes'
import './pages.css'

export function FileTransfersMainPage() {
  const { t } = useTranslation()
  const { selectedParty } = useParties()
  const organizationName = selectedParty?.name ?? ''

  return (
    <div className="page">
      <OrganizationHeader />

      {/* Keyed by actor, so a switch doesn't show the previous actor's drafts. */}
      {selectedParty && <IncompleteUploadsBanner key={selectedParty.partyUuid} party={selectedParty} />}

      <section className="page-section">
        <h2 className="page-heading">{t('page.home.services_heading')}</h2>
        <CardLink
          to={PageRoutes.services}
          title={t('page.home.services_title')}
          description={
            organizationName
              ? t('page.home.services_description_named', { name: organizationName })
              : t('page.home.services_description')
          }
        />
      </section>

      <section className="page-section">
        <h2 className="page-heading">{t('page.home.draft_heading')}</h2>
        <CardLink
          to={PageRoutes.drafts}
          title={t('page.home.draft_title')}
          description={t('page.home.draft_description')}
        />
      </section>

      <section className="page-section">
        <h2 className="page-heading">{t('page.home.active_heading')}</h2>
        <CardLink
          to={PageRoutes.active}
          title={t('page.home.active_title')}
          description={t('page.home.active_description')}
        />
      </section>

      <section className="page-section">
        <h2 className="page-heading">{t('page.home.historical_heading')}</h2>
        <CardLink
          to={PageRoutes.historical}
          title={t('page.home.historical_title')}
          description={t('page.home.historical_description')}
        />
      </section>
    </div>
  )
}
