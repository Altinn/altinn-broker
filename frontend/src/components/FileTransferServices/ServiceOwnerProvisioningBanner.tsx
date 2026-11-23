import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import type { ServiceOwnerProvisioning } from '../../api/serviceOwnerProvisioning'

type ServiceOwnerProvisioningBannerProps = {
  organizationName: string
  provisioning: ServiceOwnerProvisioning
  canStart: boolean
  starting: boolean
  error: string | null
  onStart: () => void
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
  const progressTitleId = useId()

  if (provisioning.status === 'Ready') {
    return null
  }

  if (provisioning.status === 'InProgress' || starting) {
    return (
      <div className="provision-banner provision-banner--progress" role="status" aria-live="polite" aria-busy="true">
        <span id={progressTitleId} className="provision-banner__title">
          {t('page.services.provision_progress_title', { name: organizationName })}
        </span>
        <progress aria-labelledby={progressTitleId} value={provisioning.percent} max={100} />
        <span className="provision-banner__text">
          {starting ? t('page.services.provision_starting') : t(provisioning.messageKey)}
        </span>
        {error && <span className="provision-banner__error">{error}</span>}
      </div>
    )
  }

  if (!canStart) {
    return null
  }

  return (
    <button type="button" className="provision-banner" onClick={onStart} disabled={starting}>
      <span className="provision-banner__title">
        {t('page.services.provision_banner_heading', { name: organizationName })}
      </span>
      <span className="provision-banner__text">{t('page.services.provision_banner_message')}</span>
      {error && <span className="provision-banner__error">{error}</span>}
    </button>
  )
}
