import { formatDisplayName, UserListItem } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { formatOrgNumber } from '../helpers/orgIdentifierHelper'
import { useParties } from '../parties/PartiesContext'
import './OrganizationHeader.css'

export function OrganizationHeader() {
  const { t } = useTranslation()
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return null
  }

  return (
    <div className="org-header">
      <UserListItem
        id={selectedParty.partyUuid}
        type="company"
        name={formatDisplayName({ fullName: selectedParty.name, type: 'company' })}
        description={t('page.common.org_number', { number: formatOrgNumber(selectedParty.organizationNumber) })}
        size="lg"
        titleAs="h1"
        interactive={false}
        shadow="none"
        containerAs="div"
      />
    </div>
  )
}
