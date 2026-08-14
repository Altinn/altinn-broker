import { Link, useParams } from 'react-router-dom'
import { DetailField } from '../components/DetailField'
import { formatOrganization, getActiveTransferById } from '../data/mockData'
import { PageRoutes } from './routes'
import './pages.css'

export function ActiveFileTransferDetailPage() {
  const { transferId = '' } = useParams()
  const transfer = getActiveTransferById(transferId)

  if (!transfer) {
    return <p>Fant ikke formidlingen.</p>
  }

  return (
    <div className="page">
      <div className="page-actions">
        <Link to={PageRoutes.active} className="button button--secondary">
          ← Tilbake
        </Link>
      </div>

      <section className="page-section">
        <h2 className="page-heading">{transfer.serviceName}</h2>
        <p className="page-subheading">{transfer.subtitle}</p>

        <ul className="detail-list">
          <DetailField label="Referanse" value={transfer.reference} />
          <DetailField label="Opprettet" value={transfer.createdAt} />
          <DetailField label="Avsender" value={formatOrganization(transfer.sender)} />
          <DetailField label="Mottaker" value={formatOrganization(transfer.recipient)} />
          <DetailField label="Filnavn" value={transfer.fileName} />
          {transfer.uploadedAt && <DetailField label="Opplastet" value={transfer.uploadedAt} />}
          <DetailField label="Filstørrelse" value={transfer.fileSize} />
          {transfer.virusScanned && <DetailField label="Virusskannet" value={transfer.virusScanned} />}
          {transfer.status && <DetailField label="Status" value={transfer.status} />}
        </ul>

        <div className="action-row">
          <button type="button" className="button" disabled>
            Start nedlasting
          </button>
          {transfer.statusNote && <span className="action-note">{transfer.statusNote}</span>}
        </div>

        <div className="action-row">
          <button type="button" className="button">
            Kanseller formidling
          </button>
          <span className="action-note">Kan bare utføres av Brønnøy sykehus</span>
        </div>
      </section>
    </div>
  )
}
