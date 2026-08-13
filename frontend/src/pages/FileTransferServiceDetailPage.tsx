import { Link, useParams } from 'react-router-dom'
import { CardLink } from '../components/CardLink'
import { DetailField } from '../components/DetailField'
import { getServiceById } from '../data/mockData'
import { newFileTransferPath, PageRoutes } from './routes'
import './pages.css'

export function FileTransferServiceDetailPage() {
  const { serviceId = '' } = useParams()
  const service = getServiceById(serviceId)

  if (!service) {
    return <p>Fant ikke formidlingstjenesten.</p>
  }

  return (
    <div className="page">
      <section className="page-section">
        <h2 className="page-heading">Navn og eier</h2>
        <CardLink
          to={PageRoutes.services}
          title={service.name}
          description={service.owner}
          avatarLetter={service.name[0]}
        />
      </section>

      {service.canCreate && (
        <div className="page-actions">
          <Link to={newFileTransferPath(service.id)} className="button button--secondary">
            + Opprett ny formidling
          </Link>
        </div>
      )}

      {service.lockedVariables && service.lockedVariables.length > 0 && (
        <section className="page-section">
          <h2 className="page-heading">Låste variabler for tjenesten</h2>
          <ul className="detail-list">
            {service.lockedVariables.map((variable) => (
              <DetailField key={variable.name} label={variable.name} value={variable.description} />
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
