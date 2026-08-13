import { Link } from 'react-router-dom'
import { currentOrganization } from '../data/mockData'
import './Header.css'

type HeaderProps = {
  breadcrumbs?: { label: string; to?: string }[]
}

export function Header({ breadcrumbs }: HeaderProps) {
  return (
    <header className="app-header">
      <div className="app-header__top">
        <div className="app-header__brand">
          <span className="app-header__logo" aria-hidden="true" />
          <span className="app-header__brand-text">Altinn</span>
        </div>
        <div className="app-header__profile">
          <div className="app-header__org">
            <span className="app-header__org-avatar" aria-hidden="true">
              B
            </span>
            <div>
              <div className="app-header__org-name">{currentOrganization.name}</div>
              <div className="app-header__org-number">Org.nr. {currentOrganization.orgNumber}</div>
            </div>
          </div>
          <button type="button" className="app-header__menu-button">
            Meny
          </button>
        </div>
      </div>
      {breadcrumbs && breadcrumbs.length > 0 && (
        <nav className="breadcrumbs" aria-label="Brødsmulesti">
          <ol>
            {breadcrumbs.map((item, index) => (
              <li key={item.label}>
                {item.to && index < breadcrumbs.length - 1 ? (
                  <Link to={item.to}>{item.label}</Link>
                ) : (
                  <span aria-current={index === breadcrumbs.length - 1 ? 'page' : undefined}>
                    {item.label}
                  </span>
                )}
              </li>
            ))}
          </ol>
        </nav>
      )}
    </header>
  )
}
