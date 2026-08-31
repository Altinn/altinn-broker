import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { CardLink } from '../components/CardLink'
import { OrganizationHeader } from '../components/OrganizationHeader'
import { fileTransferServices } from '../data/mockData'
import { servicePath } from './routes'
import './pages.css'

export function FileTransferServicesPage() {
  const [search, setSearch] = useState('')

  const creatable = fileTransferServices.filter((s) => s.canCreate)
  const other = fileTransferServices.filter((s) => !s.canCreate)

  const filterBySearch = (name: string) =>
    name.toLowerCase().includes(search.trim().toLowerCase())

  const filteredCreatable = useMemo(
    () => creatable.filter((s) => filterBySearch(s.name)),
    [creatable, search],
  )
  const filteredOther = useMemo(
    () => other.filter((s) => filterBySearch(s.name)),
    [other, search],
  )

  return (
    <div className="page">
      <OrganizationHeader />

      <div className="search-field">
        <label className="sr-only" htmlFor="service-search">
          Søk etter formidlingstjenester
        </label>
        <input
          id="service-search"
          type="search"
          className="input"
          placeholder="Søk etter formidlingstjenester"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {filteredCreatable.length > 0 && (
        <section className="page-section">
          <h2 className="page-heading">Formidlingstjenester Brønnøy sykehus kan opprette</h2>
          <ul className="card-list">
            {filteredCreatable.map((service) => (
              <li key={service.id}>
                <CardLink
                  to={servicePath(service.id)}
                  title={service.name}
                  description={`Eid av ${service.owner}`}
                  avatarLetter={service.name[0]}
                />
              </li>
            ))}
          </ul>
        </section>
      )}

      {filteredOther.length > 0 && (
        <section className="page-section">
          <h2 className="page-heading">Andre formidlingstjenester Brønnøy sykehus er delaktig i</h2>
          <ul className="card-list">
            {filteredOther.map((service) => (
              <li key={service.id}>
                <Link to={servicePath(service.id)} className="menu-link">
                  <span>
                    <span className="menu-link__title">{service.name}</span>
                    <span className="menu-link__description">Eid av {service.owner}</span>
                  </span>
                  <span aria-hidden="true">›</span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
