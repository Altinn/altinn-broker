import { formatDisplayName, UserListItem } from '@altinn/altinn-components'
import { formatOrgNumber } from '../helpers/orgIdentifierHelper'
import { useParties } from '../parties/PartiesContext'
import './OrganizationHeader.css'

export function OrganizationHeader() {
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
        description={`Org.nr. ${formatOrgNumber(selectedParty.organizationNumber)}`}
        size="lg"
        titleAs="h1"
        interactive={false}
        shadow="none"
        containerAs="div"
      />
    </div>
  )
}
