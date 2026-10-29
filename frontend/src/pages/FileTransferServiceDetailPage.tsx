import { Alert } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import {
  LoadingServiceDetails,
  ServiceDetails,
} from '../components/FileTransferServiceDetailPage/ServiceDetails'
import { useFileTransferService } from '../components/FileTransferServiceDetailPage/useFileTransferService'
import { useParties } from '../parties/PartiesContext'
import './pages.css'

export function FileTransferServiceDetailPage() {
  const { t } = useTranslation()
  const { serviceId = '' } = useParams()
  const { selectedParty } = useParties()
  const state = useFileTransferService(serviceId, selectedParty?.organizationNumber)

  switch (state?.status) {
    case 'loaded':
      return (
        <ServiceDetails
          {...state.service}
          onBehalfOf={selectedParty!.organizationNumber}
          sender={{
            name: selectedParty!.name,
            organizationNumber: selectedParty!.organizationNumber,
          }}
        />
      )
    case 'missing':
      return (
        <div className="page">
          <Alert
            variant="info"
            heading={t('page.service.missing_heading')}
            message={t('page.service.missing_message', { name: selectedParty?.name ?? '' })}
          />
        </div>
      )
    case 'failed':
      return (
        <div className="page">
          <Alert
            variant="danger"
            heading={t('page.service.load_failed_heading')}
            message={t('page.common.try_later')}
          />
        </div>
      )
    default:
      return <LoadingServiceDetails />
  }
}
