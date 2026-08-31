import { CardLink } from '../components/CardLink'
import { OrganizationHeader } from '../components/OrganizationHeader'
import { PageRoutes } from './routes'
import './pages.css'

export function FileTransfersMainPage() {
  return (
    <div className="page">
      <OrganizationHeader />

      <section className="page-section">
        <h2 className="page-heading">Dine formidlingstjenester</h2>
        <CardLink
          to={PageRoutes.services}
          title="Se dine formidlingstjenester"
          description="Formidlingstjenestene Brønnøy sykehus er delaktig i"
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
          description="Fullførte formidlinger Brønnøy sykehus har vært delaktig i"
        />
      </section>
    </div>
  )
}
