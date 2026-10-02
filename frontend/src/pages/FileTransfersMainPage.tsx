import { CardLink } from '../components/CardLink'
import { useParties } from '../parties/PartiesContext'
import { OrganizationHeader } from '../components/OrganizationHeader'
import { PageRoutes } from './routes'
import './pages.css'

export function FileTransfersMainPage() {
  const { selectedParty } = useParties()
  const organizationName = selectedParty?.name ?? ''

  return (
    <div className="page">
      <OrganizationHeader />

      <section className="page-section">
        <h2 className="page-heading">Dine formidlingstjenester</h2>
        <CardLink
          to={PageRoutes.services}
          title="Se dine formidlingstjenester"
          description={`Formidlingstjenestene ${organizationName} er delaktig i`}
        />
      </section>

      <section className="page-section">
        <h2 className="page-heading">Aktive formidlinger</h2>
        <CardLink
          to={PageRoutes.active}
          title="Dine aktive formidlinger"
          description="Formidlinger som pågår nå"
        />
      </section>

      <section className="page-section">
        <h2 className="page-heading">Historiske formidlinger</h2>
        <CardLink
          to={PageRoutes.historical}
          title="Historiske formidlinger"
          description="Fullførte formidlinger"
        />
      </section>
    </div>
  )
}
