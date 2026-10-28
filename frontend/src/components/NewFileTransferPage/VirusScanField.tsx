import { Field, Switch } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'

type VirusScanFieldProps = {
  checked: boolean
  /** The resource is not approved for transfers without virus scanning. */
  locked: boolean
  onChange: (checked: boolean) => void
}

/**
 * Renders nothing on a resource that is not approved for disabled virus scanning.
 */
export function VirusScanField({ checked, locked, onChange }: VirusScanFieldProps) {
  const { t } = useTranslation()
  if (locked) {
    return null
  }

  return (
    <Field>
      <Switch
        label={t('page.virus.label')}
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <Field.Description>{t('page.virus.description')}</Field.Description>
    </Field>
  )
}
