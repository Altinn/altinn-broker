import { useTranslation } from 'react-i18next'
import type { ProvisioningPhase } from '../../api/resources'
import type { ServiceOwnerProvisioning } from '../../api/serviceOwnerProvisioning'

type ServiceOwnerProvisioningBannerProps = {
  organizationName: string
  provisioning: ServiceOwnerProvisioning
  canStart: boolean
  starting: boolean
  error: string | null
  onStart: () => void
}

const phaseMessageKey: Record<ProvisioningPhase, string> = {
  Queued: 'page.services.provision_phase_queued',
  CreatingResourceGroup: 'page.services.provision_phase_resource_group',
  CreatingVirusScanStorage: 'page.services.provision_phase_virus_storage',
  EnablingMalwareProtection: 'page.services.provision_phase_malware',
  CreatingStorage: 'page.services.provision_phase_storage',
  Ready: 'page.services.provision_phase_ready',
  Failed: 'page.services.provision_failed_message',
}

/** Large call to action, or live progress, while a service owner is being provisioned. */
export function ServiceOwnerProvisioningBanner({
  organizationName,
  provisioning,
  canStart,
  starting,
  error,
  onStart,
}: ServiceOwnerProvisioningBannerProps) {
  const { t } = useTranslation()

  if (provisioning.status === 'Ready') {
    return null
  }

  if (provisioning.status === 'InProgress' || starting) {
    const phaseKey = provisioning.phase ? phaseMessageKey[provisioning.phase] : 'page.services.provision_phase_queued'
    return (
      <div className="provision-banner provision-banner--progress" role="status" aria-live="polite" aria-busy="true">
        <span className="provision-banner__title">{t('page.services.provision_progress_title', { name: organizationName })}</span>
        <progress value={provisioning.percent} max={100} />
        <span className="provision-banner__text">{starting ? t('page.services.provision_starting') : t(phaseKey)}</span>
        {error && <span className="provision-banner__error">{error}</span>}
      </div>
    )
  }

  if (!canStart) {
    return null
  }

  const failed = provisioning.status === 'Failed'
  return (
    <button type="button" className="provision-banner" onClick={onStart} disabled={starting}>
      <span className="provision-banner__title">
        {failed
          ? t('page.services.provision_failed_heading')
          : t('page.services.provision_banner_heading', { name: organizationName })}
      </span>
      <span className="provision-banner__text">
        {failed
          ? t('page.services.provision_failed_message', { name: organizationName })
          : t('page.services.provision_banner_message')}
      </span>
      {error && <span className="provision-banner__error">{error}</span>}
    </button>
  )
}
