import { useParties } from '../parties/PartiesContext'
import { formatOrganizationNumber } from '../parties/mapAuthorizedParty'
import './OrganizationHeader.css'

export function OrganizationHeader() {
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return null
  }

  return (
    <div className="org-header">
      <span className="org-header__avatar" aria-hidden="true">
        {selectedParty.name.charAt(0).toUpperCase()}
      </span>
      <div>
        <h1 className="org-header__name">{selectedParty.name}</h1>
        <p className="org-header__number">
          Org.nr. {formatOrganizationNumber(selectedParty.organizationNumber)}
        </p>
      </div>
    </div>
  )
}
