import { currentOrganization } from '../data/mockData'
import './OrganizationHeader.css'

export function OrganizationHeader() {
  return (
    <div className="org-header">
      <span className="org-header__avatar" aria-hidden="true">
        B
      </span>
      <div>
        <h1 className="org-header__name">{currentOrganization.name}</h1>
        <p className="org-header__number">Org.nr. {currentOrganization.orgNumber}</p>
      </div>
    </div>
  )
}
