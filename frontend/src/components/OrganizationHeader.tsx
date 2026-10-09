import { Avatar, Heading } from '@altinn/altinn-components'
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
      <Avatar name={selectedParty.name} type="company" size="lg" />
      <div>
        <Heading size="sm" as="h1">
          {selectedParty.name}
        </Heading>
        <p className="org-header__number">
          Org.nr. {formatOrgNumber(selectedParty.organizationNumber)}
        </p>
      </div>
    </div>
  )
}
