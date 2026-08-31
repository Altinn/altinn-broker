import { Link, useParams } from 'react-router-dom'
import { CardLink } from '../components/CardLink'
import { DetailField } from '../components/DetailField'
import { formatOrganization, getHistoricalTransferById } from '../data/mockData'
import { PageRoutes } from './routes'
import './pages.css'

export function HistoricalFileTransferDetailPage() {
  const { transferId = '' } = useParams()
  const transfer = getHistoricalTransferById(transferId)

  if (!transfer) {
    return <p>Fant ikke formidlingen.</p>
  }

  return (
    <div className="page">
      <div className="page-actions">
        <Link to={PageRoutes.historical} className="button button--secondary">
          ← Tilbake
        </Link>
      </div>

      <section className="page-section">
        <h2 className="page-heading">Navn og eier</h2>
        <CardLink
          to={PageRoutes.services}
          title={transfer.serviceName}
          description={transfer.sender.name}
          avatarLetter={transfer.serviceName[0]}
        />
      </section>

      <section className="page-section">
        <ul className="detail-list">
          <DetailField label="Referanse" value={transfer.reference} />
          <DetailField label="Opprettet" value={transfer.createdAt} />
          <DetailField label="Avsender" value={formatOrganization(transfer.sender)} />
          <DetailField label="Mottaker" value={formatOrganization(transfer.recipient)} />
          <DetailField label="Filnavn" value={transfer.fileName} />
          {transfer.uploadedAt && <DetailField label="Opplastet" value={transfer.uploadedAt} />}
          <DetailField label="Filstørrelse" value={transfer.fileSize} />
          {transfer.virusScanned && <DetailField label="Virusskannet" value={transfer.virusScanned} />}
          {transfer.downloadedAt && <DetailField label="Lastet ned" value={transfer.downloadedAt} />}
          {transfer.status && <DetailField label="Status" value={transfer.status} />}
        </ul>
      </section>
    </div>
  )
}
