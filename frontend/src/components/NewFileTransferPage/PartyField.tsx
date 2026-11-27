import { Textfield } from '@digdir/designsystemet-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'

type PartyFieldProps = {
  label: string
  description?: ReactNode
  name: string
  organizationNumber: string
}

/**
 * A party the user cannot change: the sender, or a recipient the resource has locked down.
 * Readonly rather than disabled — the value stays readable and selectable, and Designsystemet
 * marks the label with a padlock so it is clear the field is fixed rather than merely inactive.
 */
export function PartyField({ label, description, name, organizationNumber }: PartyFieldProps) {
  const { t } = useTranslation()
  return (
    <Textfield
      label={label}
      description={description}
      value={`${name} - ${t('page.common.org_number', { number: formatOrgNumber(organizationNumber) })}`}
      readOnly
    />
  )
}
