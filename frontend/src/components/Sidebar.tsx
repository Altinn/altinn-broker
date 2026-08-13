import { NavLink } from 'react-router-dom'
import { PageRoutes } from '../pages/routes'
import './Sidebar.css'

const navItems = [
  { label: 'Dine formidlingstjenester', to: PageRoutes.services },
  { label: 'Aktive formidlinger', to: PageRoutes.active },
  { label: 'Historiske formidlinger', to: PageRoutes.historical },
]

export function Sidebar() {
  return (
    <aside className="sidebar">
      <nav aria-label="Formidlinger">
        <p className="sidebar__section-title">Formidlinger</p>
        <ul className="sidebar__list">
          {navItems.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                className={({ isActive }) =>
                  `sidebar__link${isActive ? ' sidebar__link--active' : ''}`
                }
              >
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <nav aria-label="Hjelp">
        <ul className="sidebar__list sidebar__list--secondary">
          <li>
            <a className="sidebar__link" href="https://info.altinn.no/hjelp/" target="_blank" rel="noreferrer">
              Hjelpesider
            </a>
          </li>
        </ul>
      </nav>
    </aside>
  )
}
