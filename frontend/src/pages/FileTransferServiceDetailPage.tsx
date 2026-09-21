import { Alert } from '@altinn/altinn-components'
import { useParams } from 'react-router-dom'
import {
  LoadingServiceDetails,
  ServiceDetails,
} from '../components/FileTransferServiceDetailPage/ServiceDetails'
import { useFileTransferService } from '../components/FileTransferServiceDetailPage/useFileTransferService'
import { useParties } from '../parties/PartiesContext'
import './pages.css'

export function FileTransferServiceDetailPage() {
  const { serviceId = '' } = useParams()
  const { selectedParty } = useParties()
  const state = useFileTransferService(serviceId, selectedParty?.organizationNumber)

  switch (state?.status) {
    case 'loaded':
      return <ServiceDetails {...state.service} />
    case 'missing':
      return (
        <div className="page">
          <Alert
            variant="info"
            heading="Fant ikke formidlingstjenesten"
            message={`${selectedParty?.name} har ikke tilgang til denne formidlingstjenesten.`}
          />
        </div>
      )
    case 'failed':
      return (
        <div className="page">
          <Alert
            variant="danger"
            heading="Kunne ikke hente formidlingstjenesten"
            message="Prøv igjen senere."
          />
        </div>
      )
    default:
      return <LoadingServiceDetails />
  }
}
